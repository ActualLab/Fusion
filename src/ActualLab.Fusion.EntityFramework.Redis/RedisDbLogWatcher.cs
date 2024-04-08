using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Redis;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Redis;

public class RedisDbLogWatcher<TDbContext, TDbEntry> : DbLogWatcher<TDbContext, TDbEntry>
    where TDbContext : DbContext
    where TDbEntry : class, ILogEntry
{
    protected readonly ConcurrentDictionary<
        DbShard,
        LazySlim<DbShard, RedisDbLogWatcher<TDbContext, TDbEntry>, RedisPub>> RedisPubCache = new();
    protected RedisDb RedisDb { get; }

    public RedisDbLogWatcherOptions<TDbContext> Settings { get; }

    public RedisDbLogWatcher(RedisDbLogWatcherOptions<TDbContext> settings, IServiceProvider services)
        : base(services)
    {
        Settings = settings;
        RedisDb = services.GetService<RedisDb<TDbContext>>() ?? services.GetRequiredService<RedisDb>();
        var redisPub = GetRedisPub(default);
        Log.LogInformation("Using pub/sub key = '{Key}'", redisPub.FullKey);
    }

    public override async Task Notify(DbShard shard, CancellationToken cancellationToken = default)
    {
        var redisPub = GetRedisPub(shard);
        if (StopToken.IsCancellationRequested)
            return;

        await redisPub.Publish("").ConfigureAwait(false);
    }

    protected override DbShardWatcher CreateShardWatcher(DbShard shard)
        => new ShardWatcher(this, shard);

    protected RedisPub GetRedisPub(DbShard shard)
        => RedisPubCache.GetOrAdd(shard,
            static (shard1, self) => {
                var key = self.Settings.PubSubKeyFormatter.Invoke(shard1, typeof(TDbEntry));
                return self.RedisDb.GetPub(key);
            }, this);

    // Nested types

    protected class ShardWatcher : DbShardWatcher
    {
        public ShardWatcher(RedisDbLogWatcher<TDbContext, TDbEntry> owner, DbShard shard) : base(shard)
        {
            var hostId = owner.Services.GetRequiredService<HostId>();
            var key = owner.Settings.PubSubKeyFormatter.Invoke(Shard, typeof(TDbEntry));

            var watchChain = new AsyncChain($"Watch({shard})", async cancellationToken => {
                var redisSub = owner.RedisDb.GetChannelSub(key);
                await using var _ = redisSub.ConfigureAwait(false);

                await redisSub.Subscribe().ConfigureAwait(false);
                while (!cancellationToken.IsCancellationRequested) {
                    var value = await redisSub.Messages
                        .ReadAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (!StringComparer.Ordinal.Equals(hostId.Id.Value, value))
                        MarkChanged();
                }
            }).RetryForever(owner.Settings.WatchRetryDelays, owner.Log);

            _ = watchChain.RunIsolated(StopToken);
        }
    }
}
