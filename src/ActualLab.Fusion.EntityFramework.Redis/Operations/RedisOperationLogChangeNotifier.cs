using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Redis;

namespace ActualLab.Fusion.EntityFramework.Redis.Operations;

public class RedisOperationLogChangeNotifier<TDbContext>
    : DbOperationCompletionNotifierBase<TDbContext, RedisOperationLogChangeTrackingOptions<TDbContext>>
    where TDbContext : DbContext
{
    protected RedisDb RedisDb { get; }
    protected ConcurrentDictionary<DbShard, RedisPub<TDbContext>> RedisPubCache { get; }

    public RedisOperationLogChangeNotifier(
        RedisOperationLogChangeTrackingOptions<TDbContext> options,
        IServiceProvider services
        ) : base(options, services)
    {
        RedisDb = services.GetService<RedisDb<TDbContext>>() ?? services.GetRequiredService<RedisDb>();
        RedisPubCache = new();
        // ReSharper disable once VirtualMemberCallInConstructor
#pragma warning disable CA2214
        Log.LogInformation("Using pub/sub key = '{Key}'", GetRedisPub(default).FullKey);
#pragma warning restore CA2214
    }

    protected override async Task Notify(DbShard shard)
    {
        var redisPub = GetRedisPub(shard);
        await redisPub.Publish(HostId.Id.Value).ConfigureAwait(false);
    }

    protected virtual RedisPub<TDbContext> GetRedisPub(DbShard shard)
        => RedisPubCache.GetOrAdd(shard,
            static (_, state) => {
                var (self, shard1) = state;
                var key = self.Options.PubSubKeyFactory.Invoke(shard1);
                return self.RedisDb.GetPub<TDbContext>(key);
            },
            (this, shard));
}
