using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ActualLab.IO;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Npgsql;
using ActualLab.Fusion.EntityFramework.Redis;
using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Server;
using ActualLab.Fusion.Tests.Extensions;
using ActualLab.Fusion.Tests.Model;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Fusion.Tests.UIModels;
using ActualLab.Locking;
using ActualLab.Rpc;
using ActualLab.Testing.Collections;
using ActualLab.Tests;
using User = ActualLab.Fusion.Tests.Model.User;

namespace ActualLab.Fusion.Tests;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public abstract class FusionTestBase : RpcTestBase
{
    private static readonly AsyncLock InitializeLock = new(LockReentryMode.CheckedFail);

    private readonly object _lock = new();
    private IRemoteComputedCache? _remoteComputedCache;

    public FusionTestDbType DbType { get; set; } = TestRunnerInfo.IsBuildAgent()
        ? FusionTestDbType.InMemory
        : FusionTestDbType.Sqlite;
    public bool IsConsoleApp { get; set; } = false;
    public bool UseOperationLogChangeTracking { get; set; } = true;
    public bool UseRedisOperationLogChangeTracking { get; set; } = !TestRunnerInfo.IsBuildAgent();
    public bool UseInMemoryKeyValueStore { get; set; }
    public bool UseInMemoryAuthService { get; set; }
    public bool UseRemoteComputedCache { get; set; }
    public LogLevel RpcCallLogLevel { get; set; } = LogLevel.None;

    public FilePath SqliteDbPath { get; protected set; }
    public string PostgreSqlConnectionString { get; protected set; } =
        "Server=localhost;Database=fusion_tests;Port=5432;User Id=postgres;Password=postgres;Enlist=false";
    public string MariaDbConnectionString { get; protected set; } =
        "Server=localhost;Database=fusion_tests;Port=3306;User=root;Password=mariadb";
    public string SqlServerConnectionString { get; protected set; } =
        "Server=localhost,1433;Database=fusion_tests;MultipleActiveResultSets=true;TrustServerCertificate=true;User Id=sa;Password=SqlServer1";

    protected FusionTestBase(ITestOutputHelper @out) : base(@out)
    {
        var appTempDir = TestRunnerInfo.IsGitHubAction()
            ? new FilePath(Environment.GetEnvironmentVariable("RUNNER_TEMP"))
            : FilePath.GetApplicationTempDirectory("", true);
        SqliteDbPath = appTempDir & FilePath.GetHashedName($"{GetType().Name}_{GetType().Namespace}.db");
    }

    public override async Task InitializeAsync()
    {
        if (!IsConsoleApp && !DbType.IsAvailable())
            return;

        using var releaser = await InitializeLock.Lock().ConfigureAwait(false);
        releaser.MarkLockedLocally();

        for (var i = 0; i < 10 && File.Exists(SqliteDbPath); i++) {
            try {
                File.Delete(SqliteDbPath);
                break;
            }
            catch {
                await Delay(0.3);
            }
        }

        await using var dbContext = await CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        try {
            await dbContext.Database.EnsureCreatedAsync();
        }
        catch {
            // Intended - somehow it fails on GitHub build agent
        }
        if (!IsConsoleApp)
            Out.WriteLine("DB is recreated.");
        await Services.HostedServices().Start();
    }

    protected virtual bool MustSkip()
        => !DbType.IsAvailable();

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        var fusion = services.AddFusion();
        var rpc = fusion.Rpc;
        if (!isClient) {
            fusion.AddService<ITimeService, TimeService>();
            rpc.Service<ITimeService>().Remove();
            rpc.Service<ITimeServer>().HasServer<ITimeService>().HasName(nameof(ITimeService));
            fusion.AddService<IUserService, UserService>();
            fusion.AddService<IScreenshotService, ScreenshotService>();
            fusion.AddService<IEdgeCaseService, EdgeCaseService>();
            fusion.AddService<IKeyValueService<string>, KeyValueService<string>>();
        } else {
            services.AddSingleton<RpcPeerFactory>(_ => (hub, peerRef)
                => peerRef.IsServer
                    ? new RpcServerPeer(hub, peerRef) { CallLogLevel = RpcCallLogLevel }
                    : new RpcClientPeer(hub, peerRef) { CallLogLevel = RpcCallLogLevel });
            if (UseRemoteComputedCache)
                services.AddSingleton(c => {
                    lock (_lock) {
                        return _remoteComputedCache ??=
                            new InMemoryRemoteComputedCache(InMemoryRemoteComputedCache.Options.Default, c);
                    }
                });
            fusion.AddClient<ITimeService>();
            fusion.AddClient<IUserService>();
            fusion.AddClient<IScreenshotService>();
            fusion.AddClient<IEdgeCaseService>();
            fusion.AddClient<IKeyValueService<string>>();
        }
        services.AddSingleton<UserService>();
        services.AddSingleton<ComputedState<ServerTimeModel1>, ServerTimeModel1State>();
        services.AddSingleton<ComputedState<KeyValueModel<string>>, StringKeyValueModelState>();
        fusion.AddService<ISimplestProvider, SimplestProvider>(ServiceLifetime.Scoped);
        fusion.AddService<NestedOperationLoggerTester>();
    }

    protected override void ConfigureServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureServices(services, isClient);

        // Core Fusion services
        var fusion = services.AddFusion();
        fusion.AddOperationReprocessor();
        fusion.AddFusionTime();

        if (!isClient) {
            fusion = fusion.WithServiceMode(RpcServiceMode.Server, true);
            var fusionServer = fusion.AddWebServer();
#if !NETFRAMEWORK
            fusionServer.AddMvc().AddControllers();
#endif
            if (UseInMemoryAuthService)
                fusion.AddInMemoryAuthService();
            else
                fusion.AddDbAuthService<TestDbContext, DbAuthSessionInfo, DbAuthUser, long>();
            if (UseInMemoryKeyValueStore)
                fusion.AddInMemoryKeyValueStore();
            else
                fusion.AddDbKeyValueStore<TestDbContext>();

            // DbContext & related services
            services.AddPooledDbContextFactory<TestDbContext>(db => {
                switch (DbType) {
                case FusionTestDbType.Sqlite:
                    db.UseSqlite($"Data Source={SqliteDbPath}");
                    break;
                case FusionTestDbType.InMemory:
                    db.UseInMemoryDatabase(SqliteDbPath)
                        .ConfigureWarnings(warnings => {
                            warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning);
                        });
                    break;
                case FusionTestDbType.PostgreSql:
                    db.UseNpgsql(PostgreSqlConnectionString, npgsql => {
                        npgsql.EnableRetryOnFailure(0);
                    });
                    db.UseNpgsqlHintFormatter();
                    break;
                case FusionTestDbType.MariaDb:
#if NET5_0_OR_GREATER || NETCOREAPP
                    var serverVersion = ServerVersion.AutoDetect(MariaDbConnectionString);
                    db.UseMySql(MariaDbConnectionString, serverVersion, mySql => {
#else
                    db.UseMySql(MariaDbConnectionString, mySql => {
#endif
                        mySql.EnableRetryOnFailure(0);
                    });
                    break;
                case FusionTestDbType.SqlServer:
                    db.UseSqlServer(SqlServerConnectionString, sqlServer => {
                        sqlServer.EnableRetryOnFailure(0);
                    });
                    break;
                default:
                    throw new NotSupportedException();
                }
                db.EnableSensitiveDataLogging();
            });
            services.AddDbContextServices<TestDbContext>(db => {
                var useRedis = UseOperationLogChangeTracking && UseRedisOperationLogChangeTracking;
                if (useRedis)
                    db.AddRedisDb("localhost", "Fusion.Tests");
                db.AddOperations(operations => {
                    if (!UseOperationLogChangeTracking)
                        return;

                    operations.ConfigureOperationLogReader(_ => new() {
                        // Enable this if you debug multi-host invalidation
                        // MaxCommitDuration = TimeSpan.FromMinutes(5),
                    });
                    if (useRedis)
                        operations.AddRedisOperationLogWatcher();
                    else if (DbType == FusionTestDbType.PostgreSql)
                        operations.AddNpgsqlOperationLogWatcher();
                    else
                        operations.AddFileSystemOperationLogWatcher();
                });
                db.AddEntityResolver<long, User>();
            });
        }
        else {
            fusion.AddAuthClient();

            // Custom computed state
            services.AddSingleton(c => c.StateFactory().NewComputed<ServerTimeModel2>(
                new() { InitialValue = new(default) },
                async (_, cancellationToken) => {
                    var client = c.GetRequiredService<ITimeService>();
                    var time = await client.GetTime(cancellationToken).ConfigureAwait(false);
                    return new ServerTimeModel2(time);
                }));
        }
    }

    protected ValueTask<TestDbContext> CreateDbContext(CancellationToken cancellationToken = default)
        => Services.GetRequiredService<DbHub<TestDbContext>>().CreateDbContext(readWrite: true, cancellationToken);
}
