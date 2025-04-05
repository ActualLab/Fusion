using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ActualLab.Fusion.Blazor;
using ActualLab.Fusion.Blazor.Authentication;
using Microsoft.Extensions.Configuration.Memory;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Npgsql;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.Server;
using ActualLab.Interception;
using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using ActualLab.Rpc.Testing;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Samples.TodoApp;
using Samples.TodoApp.Abstractions;
using Samples.TodoApp.Host;
using Samples.TodoApp.Host.Components;
using Samples.TodoApp.Host.Components.Pages;
using Samples.TodoApp.Services;
using Samples.TodoApp.Services.Db;
using Samples.TodoApp.UI;

// Constrain thread pool to 1 thread to debug possible issues with async logic
// ThreadPool.SetMinThreads(1, 1);
// ThreadPool.SetMaxThreads(1, 1);

// IComputeService validation should be off in release
#if !DEBUG
Interceptor.Options.Defaults.IsValidationEnabled = false;
#endif
// Actions below should be taken as early as possible:
// Activity.DefaultIdFormat = ActivityIdFormat.W3C;
DbShardResolver.DefaultSessionShardTag = TenantExt.SessionTag;

var builder = WebApplication.CreateBuilder();
var env = builder.Environment;
var cfg = builder.Configuration;
var hostSettings = cfg.GetSettings<HostSettings>();
if (hostSettings.IsAspireManaged)
    builder.AddServiceDefaults();
TenantExt.UseTenants = hostSettings.UseTenants;
var hostKind = hostSettings.HostKind;
Console.WriteLine($"Host kind: {hostKind}");

cfg.Sources.Insert(0, new MemoryConfigurationSource() {
    InitialData = new Dictionary<string, string>(StringComparer.Ordinal) {
        { WebHostDefaults.ServerUrlsKey, $"http://localhost:{hostSettings.Port ?? 5005}" }, // Override default server URLs
    }!
});

// Configure services
var services = builder.Services;
ConfigureLogging();
ConfigureServices();
builder.WebHost.UseDefaultServiceProvider((ctx, options) => {
    if (ctx.HostingEnvironment.IsDevelopment()) {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    }
});

// Build & configure app
var app = builder.Build();
StaticLog.Factory = app.Services.LoggerFactory();
var log = StaticLog.For<Program>();
ConfigureApp();

// Ensure the DB is created
if (hostKind != HostKind.ApiServer) { // This has to be done in a more robust way in a real app - e.g. by a sidecar container
    var dbContextFactory = app.Services.GetRequiredService<IShardDbContextFactory<AppDbContext>>();
    var shardRegistry = app.Services.GetRequiredService<IDbShardRegistry<AppDbContext>>();
    foreach (var shard in shardRegistry.Shards.Value) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(shard);
        if (hostSettings.MustRecreateDb)
            await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }
}

// Run the app
await app.RunAsync();
return;

// Helpers

void ConfigureLogging()
{
    // Logging
    services.AddLogging(logging => {
        // Use appsettings.*.json to change log filters
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Debug);
    });
}

void ConfigureServices()
{
    services.AddSingleton(hostSettings);

    // DbContext & related services
    DbOperationScope.Options.DefaultIsolationLevel = IsolationLevel.RepeatableRead;
    if (hostKind != HostKind.ApiServer) // ApiServer doesn't need DB
        services.AddDbContextServices<AppDbContext>(db => {
            // Uncomment if you'll be using AddRedisOperationLogWatcher
            // db.AddRedisDb("localhost", "Fusion.Samples.TodoApp");
            db.AddOperations(operations => {
                operations.ConfigureOperationLogReader(_ => new() {
                    // We use AddFileSystemOperationLogWatcher, so unconditional check period
                    // can be arbitrary long - all depends on the reliability of Notifier-Watcher chain.
                    CheckPeriod = TimeSpan.FromSeconds(env.IsDevelopment() ? 60 : 5),
                });
                operations.ConfigureEventLogReader(_ => new() {
                    CheckPeriod = TimeSpan.FromSeconds(env.IsDevelopment() ? 60 : 5),
                });
                if (!hostSettings.UsePostgreSql.IsNullOrEmpty())
                    operations.AddNpgsqlOperationLogWatcher();
                else
                    operations.AddFileSystemOperationLogWatcher();
                // operations.AddRedisOperationLogWatcher();
            });
            db.AddEntityResolver<string, DbTodo>();

            if (hostSettings.UseTenants) {
                db.AddSharding(sharding => {
                    var tenantIndexes = hostSettings.TenantIndex is { } tenantIndex
                        ? Enumerable.Range(tenantIndex, 1) // Serve just a single tenant
                        : Enumerable.Range(0, hostSettings.TenantCount); // All tenants are served
                    sharding.AddShardRegistry(tenantIndexes.Select(i => $"tenant{i}"));
                    sharding.AddTransientShardDbContextFactory(ConfigureShardDbContext);
                });
            }
            else {
                // ReSharper disable once VariableHidesOuterVariable
                db.Services.AddTransientDbContextFactory<AppDbContext>((c, db) => {
                    // We use fakeShard here solely to be able to
                    // re-use the configuration logic from ConfigureShardDbContext.
                    ConfigureShardDbContext(c, DbShard.Single, db);
                });
            }
        });

    // Fusion services
    var fusion = services.AddFusion(RpcServiceMode.Server, true);
    var fusionServer = fusion.AddWebServer(hostKind == HostKind.BackendServer);
#if false
    // Enable this to test how the client behaves w/ a delay
    fusion.Rpc.AddInboundMiddleware(c => new RpcRandomDelayMiddleware(c) {
        Delay = new(1, 0.1),
    });
#endif

    if (hostKind == HostKind.ApiServer) {
        fusion.AddClient<IAuth>(); // IAuth = a client of backend's IAuth
        fusion.AddClient<IAuthBackend>(); // IAuthBackend = a client of backend's IAuthBackend
        fusion.Rpc.Service<IAuth>().HasServer<IAuth>(); // Expose IAuth (a client) via RPC
    }
    else { // SingleServer or BackendServer
        fusion.AddOperationReprocessor();
        fusion.AddDbAuthService<AppDbContext, string>();
        if (hostKind == HostKind.BackendServer)
            fusion.Rpc.Service<IAuthBackend>().HasServer(); // Expose IAuthBackend via RPC
    }

    if (hostSettings.UseTenants) {
        var tenantTagExtractor = hostSettings.TenantIndex is { } tenantIndex
            ? _ => $"tenant{tenantIndex}"
            : TenantExt.CreateTagExtractor(hostSettings.Tenant0Port, hostSettings.TenantCount, hostSettings.Port);
        fusionServer.ConfigureSessionMiddleware(c => new() {
            TagProvider = (session, httpContext) => session.WithTag(TenantExt.SessionTag, tenantTagExtractor.Invoke(httpContext)),
        });
    }
    fusionServer.ConfigureAuthEndpoint(_ => new() {
        DefaultSignInScheme = MicrosoftAccountDefaults.AuthenticationScheme,
        SignInPropertiesBuilder = (_, properties) => {
            properties.IsPersistent = true;
        }
    });
    fusionServer.ConfigureServerAuthHelper(_ => new() {
        NameClaimKeys = [],
    });

    // ITodoBackend
    _ = hostKind switch {
        HostKind.SingleServer => fusion.AddComputeService<ITodoBackend, TodoBackend>(),
        HostKind.BackendServer => fusion.AddServer<ITodoBackend, TodoBackend>(),
        HostKind.ApiServer => fusion.AddClient<ITodoBackend>(),
        _ => throw Errors.InternalError("Invalid host kind."),
    };

    // ITodoApi and ISimpleService
    if (hostKind is HostKind.ApiServer or HostKind.SingleServer) {
        fusion.AddServer<ITodoApi, TodoApi>();
        // fusion.AddServer<ITodoApi, InMemoryTodoApi>(); // Simpler in-memory alternative to TodoApi
        fusion.Rpc.AddServer<ISimpleService, SimpleService>();
    }

    // Shared services
    ClientStartup.ConfigureSharedServices(services, hostKind, hostSettings.BackendUrl);

    // ASP.NET Core authentication providers
    services.AddAuthentication(options => {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    }).AddCookie(options => {
        options.LoginPath = "/signIn";
        options.LogoutPath = "/signOut";
        if (env.IsDevelopment())
            options.Cookie.SecurePolicy = CookieSecurePolicy.None;
        // This controls the expiration time stored in the cookie itself
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        // And this controls when the browser forgets the cookie
        options.Events.OnSigningIn = ctx => {
            ctx.CookieOptions.Expires = DateTimeOffset.UtcNow.AddDays(28);
            return Task.CompletedTask;
        };
    }).AddMicrosoftAccount(options => {
        options.ClientId = hostSettings.MicrosoftAccountClientId;
        options.ClientSecret = hostSettings.MicrosoftAccountClientSecret;
        // That's for personal account authentication flow
        options.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
        options.TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    }).AddGitHub(options => {
        options.ClientId = hostSettings.GitHubClientId;
        options.ClientSecret = hostSettings.GitHubClientSecret;
        options.Scope.Add("read:user");
        options.Scope.Add("user:email");
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    });

    // Web
    // services.AddMvc().AddApplicationPart(Assembly.GetExecutingAssembly());
    services.AddServerSideBlazor(o => {
        o.DetailedErrors = true;
        // Just to test TodoUI disposal handling - you shouldn't use settings like this in production:
        o.DisconnectedCircuitMaxRetained = 1;
        o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(30);
    });
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents()
        .AddInteractiveWebAssemblyComponents();
    fusion.AddBlazor().AddAuthentication().AddPresenceReporter(); // Must follow services.AddServerSideBlazor()!
    services.AddBlazorCircuitActivitySuppressor();
}

// ReSharper disable once VariableHidesOuterVariable
void ConfigureShardDbContext(IServiceProvider services, string shard, DbContextOptionsBuilder db)
{
    if (!string.IsNullOrEmpty(hostSettings.UseSqlServer))
        db.UseSqlServer(hostSettings.UseSqlServer.Interpolate(shard));
    else if (!string.IsNullOrEmpty(hostSettings.UsePostgreSql)) {
        db.UseNpgsql(hostSettings.UsePostgreSql.Interpolate(shard), npgsql => {
            npgsql.EnableRetryOnFailure(0);
        });
        db.UseNpgsqlHintFormatter();
    }
    else {
        var appTempDir = FilePath.GetApplicationTempDirectory("", true);
        var dbPath = (appTempDir & "TodoApp_v1_{0:StorageId}.db").Value.Interpolate(shard);
        db.UseSqlite($"Data Source={dbPath}");
    }
    if (env.IsDevelopment())
        db.EnableSensitiveDataLogging();
}

void ConfigureApp()
{
    // Configure the HTTP request pipeline
    StaticWebAssetsLoader.UseStaticWebAssets(env, cfg);
    if (app.Environment.IsDevelopment()) {
        app.UseWebAssemblyDebugging();
    }
    else {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
    app.UseHttpsRedirection();
    app.UseWebSockets(new WebSocketOptions() {
        KeepAliveInterval = TimeSpan.FromSeconds(30),
    });
    app.UseFusionSession();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAntiforgery();

    // Change Blazor Server culture
    app.Use(async (_, next) => {
        var culture = CultureInfo.CreateSpecificCulture("fr-FR");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        await next().ConfigureAwait(false);
    });

    // Razor components
    app.MapStaticAssets();
    app.MapRazorComponents<_HostPage>()
        .AddInteractiveServerRenderMode()
        .AddInteractiveWebAssemblyRenderMode()
        .AddAdditionalAssemblies(typeof(App).Assembly);

    // Fusion endpoints
    app.MapRpcWebSocketServer();
    app.MapFusionAuthEndpoints();
    app.MapFusionRenderModeEndpoints();
}
