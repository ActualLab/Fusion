using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartOPR;

// ============================================================================
// PartO-PR.md snippets: Log Watchers
// ============================================================================

public class AppDbContext(DbContextOptions options) : DbContextBase(options)
{
    public DbSet<DbOperation> Operations => Set<DbOperation>();
}

public class NpgsqlWatcherSetup
{
    #region PartOPR_NpgsqlSetup
    // services.AddDbContextServices<AppDbContext>(db => {
    //     db.AddOperations(operations => {
    //         operations.AddNpgsqlOperationLogWatcher();
    //     });
    // });
    #endregion

    #region PartOPR_NpgsqlConfiguration
    // operations.AddNpgsqlOperationLogWatcher(_ => new() {
    //     ChannelNameFormatter = (shard, entryType) =>
    //         $"myapp_{entryType.Name}{(shard.IsNone() ? "" : $"_{shard}")}",
    //     TrackerRetryDelays = RetryDelaySeq.Exp(1, 10),
    // });
    #endregion
}

public class RedisWatcherSetup
{
    #region PartOPR_RedisSetup
    // services.AddDbContextServices<AppDbContext>(db => {
    //     // First, configure Redis connection
    //     db.AddRedisDb("localhost:6379", "MyApp");
    //
    //     db.AddOperations(operations => {
    //         operations.AddRedisOperationLogWatcher();
    //     });
    // });
    #endregion

    #region PartOPR_RedisConfiguration
    // operations.AddRedisOperationLogWatcher(_ => new() {
    //     PubSubKeyFormatter = (shard, entryType) =>
    //         $"myapp.{entryType.Name}{(shard.IsNone() ? "" : $".{shard}")}",
    //     WatchRetryDelays = RetryDelaySeq.Exp(1, 10),
    // });
    #endregion
}

public class FileSystemWatcherSetup
{
    #region PartOPR_FileSystemSetup
    // services.AddDbContextServices<AppDbContext>(db => {
    //     db.AddOperations(operations => {
    //         operations.AddFileSystemOperationLogWatcher();
    //     });
    // });
    #endregion

    #region PartOPR_FileSystemConfiguration
    // operations.AddFileSystemOperationLogWatcher(_ => new() {
    //     FilePathFormatter = (shard, entryType) =>
    //         Path.Combine(
    //             Path.GetTempPath(),
    //             $"myapp_{entryType.Name}{(shard.IsNone() ? "" : $"_{shard}")}.tracker"),
    // });
    #endregion
}

public class FakeWatcherExample
{
    #region PartOPR_FakeWatcher
    // When you don't configure any watcher:
    // db.AddOperations(operations => {
    //     // No AddXxxOperationLogWatcher call
    //     // FakeDbLogWatcher is used automatically
    // });
    #endregion
}

public class MultipleWatchersExample
{
    #region PartOPR_MultipleWatchers
    // db.AddOperations(operations => {
    //     operations.AddFileSystemOperationLogWatcher();  // Overwritten
    //     operations.AddNpgsqlOperationLogWatcher();       // This one is used
    // });
    #endregion
}

#region PartOPR_CustomWatcher
// You can implement your own watcher for other message brokers:
// public class KafkaDbLogWatcher<TDbContext, TDbEntry> : DbLogWatcher<TDbContext, TDbEntry>
//     where TDbContext : DbContext
// {
//     // Implement NotifyChanged and WhenChanged
//     // using Kafka publish/subscribe
// }
#endregion

public class MonitoringExamples
{
    #region PartOPR_CheckWatcherRegistration
    // var watcher = services.GetService<IDbLogWatcher<AppDbContext, DbOperation>>();
    // Console.WriteLine(watcher?.GetType().Name ?? "No watcher registered");
    #endregion

    #region PartOPR_EnableTracing
    // operations.ConfigureOperationLogReader(_ => new() {
    //     IsTracingEnabled = true,  // Enables Activity tracing
    // });
    #endregion
}
