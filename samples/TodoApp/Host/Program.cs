using System.Data;
using System.Globalization;
using System.Reflection;
using ActualLab.DependencyInjection;
using ActualLab.Fusion.Blazor;
using ActualLab.Fusion.Blazor.Authentication;
using Microsoft.Extensions.Configuration.Memory;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Npgsql;
using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Server;
using ActualLab.Fusion.Server.Middlewares;
using ActualLab.IO;
using ActualLab.Multitenancy;
using ActualLab.OS;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Templates.TodoApp.Abstractions;
using Templates.TodoApp.Host;
using Templates.TodoApp.Services;
using Templates.TodoApp.UI;

// Constrain thread pool to 1 thread to debug possible issues with async logic
// ThreadPool.SetMinThreads(1, 1);
// ThreadPool.SetMaxThreads(1, 1);

// IComputeService validation should be off in release
#if !DEBUG
InterceptorBase.Options.Defaults.IsValidationEnabled = false;
#endif

var builder = WebApplication.CreateBuilder();
var env = builder.Environment;
var cfg = builder.Configuration;

// Set default server URLs
cfg.Sources.Insert(0, new MemoryConfigurationSource() {
    InitialData = new Dictionary<string, string>() {
        { WebHostDefaults.ServerUrlsKey, "http://localhost:5005;http://localhost:5006" },
    }!
});

// Configure services
var hostSettings = cfg.GetSettings<HostSettings>();
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
ConfigureApp();

// Ensure the DB is created
var dbContextFactory = app.Services.GetRequiredService<IMultitenantDbContextFactory<AppDbContext>>();
var tenantRegistry = app.Services.GetRequiredService<ITenantRegistry>();
foreach (var tenant in tenantRegistry.AllTenants.Values) {
    await using var dbContext = dbContextFactory.CreateDbContext(tenant);
    // await dbContext.Database.EnsureDeletedAsync();
    await dbContext.Database.EnsureCreatedAsync();
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
    services.AddSingleton(_ => new DbOperationScope<AppDbContext>.Options() {
        DefaultIsolationLevel = IsolationLevel.RepeatableRead,
    });
    services.AddDbContextServices<AppDbContext>(db => {
        // Uncomment if you'll be using AddRedisOperationLogChangeTracking
        // db.AddRedisDb("localhost", "Fusion.Samples.TodoApp");
        db.AddOperations(operations => {
            operations.ConfigureOperationLogReader(_ => new() {
                // We use FileBasedDbOperationLogChangeTracking, so unconditional wake up period
                // can be arbitrary long - all depends on the reliability of Notifier-Monitor chain.
                UnconditionalCheckPeriod = TimeSpan.FromSeconds(env.IsDevelopment() ? 60 : 5),
            });
            operations.AddFileBasedOperationLogChangeTracking();
            // db.AddRedisOperationLogChangeTracking();
        });

        if (hostSettings.UseMultitenancy) {
            db.AddMultitenancy(multitenancy => {
                multitenancy.UseMultitenantMode();
                multitenancy.AddMultitenantRegistry(
                    Enumerable.Range(0, 3).Select(i => new Tenant($"tenant{i}")));
                multitenancy.AddMultitenantTransientDbContextFactory(ConfigureTenantDbContext);
                // This call allows similar blocks for DbContext-s to call "UseDefault"
                // to make them re-use the same multitenancy settings (registry, resolver, etc.)
                multitenancy.MakeDefault();
            });
        }
        else {
            // ReSharper disable once VariableHidesOuterVariable
            db.Services.AddTransientDbContextFactory<AppDbContext>((c, db) => {
                // We use fakeTenant here solely to be able to
                // re-use the configuration logic from
                // ConfigureTenantDbContext.
                var fakeTenant = new Tenant(default, "single", "single");
                ConfigureTenantDbContext(c, fakeTenant, db);
            });
        }
    });

    // Fusion services
    var fusion = services.AddFusion(RpcServiceMode.Server, true);
    var fusionServer = fusion.AddWebServer();
    // You may comment this out - the call below just enables RPC call logging
    services.AddSingleton<RpcPeerFactory>(_ =>
        static (hub, peerRef) => !peerRef.IsServer
            ? throw new NotSupportedException("No client peers are allowed on the server.")
            : new RpcServerPeer(hub, peerRef) { CallLogLevel = LogLevel.Debug }
    );
#if false
    // Enable this to test how the client behaves w/ a delay
    fusion.Rpc.AddInboundMiddleware(c => new RpcRandomDelayMiddleware(c) {
        Delay = new(0.1, 0.05),
    });
#endif

    // fusionServer.AddMvc().AddControllers();
    if (hostSettings.UseInMemoryAuthService)
        fusion.AddInMemoryAuthService();
    else
        fusion.AddDbAuthService<AppDbContext, string>();
    fusion.AddDbKeyValueStore<AppDbContext>();

    if (hostSettings.UseMultitenancy)
        fusionServer.ConfigureSessionMiddleware(_ => new() {
            TenantIdExtractor = TenantIdExtractors.FromSubdomain(".localhost")
                .Or(TenantIdExtractors.FromPort((5005, 5010)))
                .WithValidator(tenantId => tenantId.Value.StartsWith("tenant")),
        });
    fusionServer.ConfigureAuthEndpoint(_ => new() {
        DefaultSignInScheme = MicrosoftAccountDefaults.AuthenticationScheme,
        SignInPropertiesBuilder = (_, properties) => {
            properties.IsPersistent = true;
        }
    });
    fusionServer.ConfigureServerAuthHelper(_ => new() {
        NameClaimKeys = Array.Empty<string>(),
    });
    fusion.AddSandboxedKeyValueStore();
    fusion.AddOperationReprocessor();

    // Compute service(s)
    fusion.AddService<ITodos, Todos>();

    // Shared services
    StartupHelper.ConfigureSharedServices(services);

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
    services.AddRazorPages();
#if NET8_0_OR_GREATER
    services.AddRazorComponents();
#endif
    fusion.AddBlazor().AddAuthentication().AddPresenceReporter(); // Must follow services.AddServerSideBlazor()!
}

// ReSharper disable once VariableHidesOuterVariable
void ConfigureTenantDbContext(IServiceProvider services, Tenant tenant, DbContextOptionsBuilder db)
{
    if (!string.IsNullOrEmpty(hostSettings.UseSqlServer))
        db.UseSqlServer(hostSettings.UseSqlServer.Interpolate(tenant));
    else if (!string.IsNullOrEmpty(hostSettings.UsePostgreSql)) {
        db.UseNpgsql(hostSettings.UsePostgreSql.Interpolate(tenant), npgsql => {
            npgsql.EnableRetryOnFailure(0);
        });
        db.UseNpgsqlHintFormatter();
    }
    else {
        var appTempDir = FilePath.GetApplicationTempDirectory("", true);
        var dbPath = (appTempDir & "App_{0:StorageId}.db").Value.Interpolate(tenant);
        db.UseSqlite($"Data Source={dbPath}");
    }
    if (env.IsDevelopment())
        db.EnableSensitiveDataLogging();
}

void ConfigureApp()
{
    // This server serves static content from Blazor Client,
    // and since we don't copy it to local wwwroot,
    // we need to find Client's wwwroot in bin/(Debug/Release) folder
    // and set it as this server's content root.
    var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
    var wwwRootPath = Path.Combine(baseDir, "wwwroot");
    var dotNetDir = $"net{RuntimeInfo.DotNetCore.Version?.Major ?? 8}.0";
    if (!Directory.Exists(Path.Combine(wwwRootPath, "_framework")))
        // This is a regular build, not a build produced w/ "publish",
        // so we remap wwwroot to the client's wwwroot folder
        wwwRootPath = Path.GetFullPath(Path.Combine(baseDir, $"../../UI/{dotNetDir}/wwwroot"));
    env.WebRootPath = wwwRootPath;
    env.WebRootFileProvider = new PhysicalFileProvider(env.WebRootPath);
    StaticWebAssetsLoader.UseStaticWebAssets(env, cfg);
    if (env.IsDevelopment()) {
        app.UseDeveloperExceptionPage();
        app.UseWebAssemblyDebugging();
    }
    else {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }
    app.UseHttpsRedirection();
    app.UseWebSockets(new WebSocketOptions() {
        KeepAliveInterval = TimeSpan.FromSeconds(30),
    });
    app.UseFusionSession();

    // Change Blazor Server culture
    app.Use(async (_, next) => {
        var culture = CultureInfo.CreateSpecificCulture("fr-FR");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        await next().ConfigureAwait(false);
    });

    // Blazor + static files
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    // API controllers
    app.UseRouting();
    app.UseAuthentication();
#pragma warning disable ASP0014
    app.UseEndpoints(endpoints => {
        endpoints.MapBlazorHub();
        endpoints.MapRpcWebSocketServer();
        endpoints.MapFusionAuth();
        endpoints.MapFusionBlazorMode();
        // endpoints.MapControllers();
        endpoints.MapFallbackToPage("/_Host");
    });
#pragma warning restore ASP0014
}
