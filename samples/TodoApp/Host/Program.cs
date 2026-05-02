using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ActualLab.Fusion.Blazor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
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
using ActualLab.Rpc.Middlewares;
using ActualLab.Rpc.Server;
using ActualLab.Rpc.Testing;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Samples.TodoApp;
using Samples.TodoApp.Abstractions;
using Samples.TodoApp.Host;
using Samples.TodoApp.Host.Components;
using Samples.TodoApp.Host.Components.Pages;
using Samples.TodoApp.Services;
using Samples.TodoApp.Services.Auth;
using Samples.TodoApp.Services.Db;
using Samples.TodoApp.UI;
using Samples.TodoApp.UI.Services;

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
            db.AddEntityResolver<string, DbSessionInfo>();
            db.AddEntityResolver<string, DbUser>(_ => new DbEntityResolver<AppDbContext, string, DbUser>.Options() {
                QueryTransformer = query => query.Include(u => u.Identities),
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
    fusion.Rpc.AddMiddleware(_ => new RpcInboundCallDelayer() { Delay = new(1, 0.1) });
#endif

    if (hostKind == HostKind.ApiServer) {
        fusion.AddClient<IUserBackend>();
        fusion.AddClient<ISessionBackend>();
    }
    else { // SingleServer or BackendServer
        fusion.AddOperationReprocessor();
        fusion.AddServer<ISessionBackend, SessionBackend>();
        fusion.AddServer<IUserBackend, UserBackend>();
    }

    // Auth endpoints & helper (server-side)
    if (hostKind != HostKind.BackendServer) {
        fusion.AddServer<IUserApi, UserApi>();
        services.AddSingleton(c => (ISessionValidator)c.GetRequiredService<IUserApi>());
        services.AddSingleton(_ => ServerAuthHelper.Options.Default with { NameClaimKeys = [] });
        services.AddScoped(c => new ServerAuthHelper(c.GetRequiredService<ServerAuthHelper.Options>(), c));
        services.AddSingleton(_ => new AuthEndpoints.Options() {
            DefaultSignInScheme = GitHubAuthenticationDefaults.AuthenticationScheme,
            SignInPropertiesBuilder = (_, properties) => {
                properties.IsPersistent = true;
            },
        });
        services.AddSingleton(c => new AuthEndpoints(c.GetRequiredService<AuthEndpoints.Options>()));
    }

    if (hostSettings.UseTenants) {
        var tenantTagExtractor = hostSettings.TenantIndex is { } tenantIndex
            ? _ => $"tenant{tenantIndex}"
            : TenantExt.CreateTagExtractor(hostSettings.Tenant0Port, hostSettings.TenantCount, hostSettings.Port);
        fusionServer.ConfigureSessionMiddleware(c => new() {
            TagProvider = (session, httpContext) => session.WithTag(TenantExt.SessionTag, tenantTagExtractor.Invoke(httpContext)),
        });
    }

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

        // IStockApi - real-time stock ticker demo
        fusion.AddServer<IStockApi, InMemoryStockApi>();
        services.AddHostedService(c => (InMemoryStockApi)c.GetRequiredService<IStockApi>());
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
#if false // Disabled for now, MicrosoftAccountClientXxx settings have to be updated
    }).AddMicrosoftAccount(options => {
        options.ClientId = hostSettings.MicrosoftAccountClientId;
        options.ClientSecret = hostSettings.MicrosoftAccountClientSecret;
        // That's for personal account authentication flow
        options.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
        options.TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
#endif
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

    // Local Blazor auth (replaces fusion.AddBlazor().AddAuthentication().AddPresenceReporter())
    fusion.AddBlazor();
    services.AddAuthorizationCore();
    services.RemoveAll(typeof(AuthenticationStateProvider));
    services.AddSingleton(_ => AuthStateProvider.Options.Default);
    services.AddScoped<AuthenticationStateProvider>(c => new AuthStateProvider(
        c.GetRequiredService<AuthStateProvider.Options>(), c));
    services.AddScoped(c => (AuthStateProvider)c.GetRequiredService<AuthenticationStateProvider>());
    services.AddScoped(c => new ClientAuthHelper(c));
    services.AddSingleton(_ => PresenceReporter.Options.Default);
    services.AddScoped(c => new PresenceReporter(c.GetRequiredService<PresenceReporter.Options>(), c));
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

    // Auth endpoints
    if (hostKind != HostKind.BackendServer) {
        app.MapFusionRenderModeEndpoints();
        var authEndpoints = app.Services.GetRequiredService<AuthEndpoints>();
        app.MapGet("/signIn", authEndpoints.SignIn).WithGroupName("FusionAuth");
        app.MapGet("/signIn/{scheme}", authEndpoints.SignIn).WithGroupName("FusionAuth");
        app.MapGet("/signOut", authEndpoints.SignOut).WithGroupName("FusionAuth");
    }
}
