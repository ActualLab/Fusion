using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Redis;

namespace ActualLab.Fusion.EntityFramework.Redis.Operations;

public class RedisOperationLogChangeTracker<TDbContext>
    : DbOperationCompletionTrackerBase<TDbContext, RedisOperationLogChangeTrackingOptions<TDbContext>>
    where TDbContext : DbContext
{
    protected RedisDb RedisDb { get; }

    public RedisOperationLogChangeTracker(
        RedisOperationLogChangeTrackingOptions<TDbContext> options,
        IServiceProvider services)
        : base(options, services)
    {
        RedisDb = services.GetService<RedisDb<TDbContext>>() ?? services.GetRequiredService<RedisDb>();
        var redisPub = RedisDb.GetPub<TDbContext>(Options.PubSubKeyFactory.Invoke(default));
        Log.LogInformation("Using pub/sub key = '{Key}'", redisPub.FullKey);
    }

    protected override DbShardWatcher CreateShardWatcher(DbShard shard)
        => new ShardWatcher(this, shard);

    // Nested types

    protected class ShardWatcher : DbShardWatcher
    {
        public ShardWatcher(RedisOperationLogChangeTracker<TDbContext> owner, DbShard shard) : base(shard)
        {
            var hostId = owner.Services.GetRequiredService<HostId>();
            var key = owner.Options.PubSubKeyFactory.Invoke(Shard);

            var watchChain = new AsyncChain($"Watch({shard})", async cancellationToken => {
                var redisSub = owner.RedisDb.GetChannelSub(key);
                await using var _ = redisSub.ConfigureAwait(false);

                await redisSub.Subscribe().ConfigureAwait(false);
                while (!cancellationToken.IsCancellationRequested) {
                    var value = await redisSub.Messages
                        .ReadAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (!StringComparer.Ordinal.Equals(hostId.Id.Value, value))
                        CompleteWaitForChanges();
                }
            }).RetryForever(owner.Options.TrackerRetryDelays, owner.Log);

            _ = watchChain.RunIsolated(StopToken);
        }
    }
}
