using ActualLab.Fusion.EntityFramework.Operations;

namespace ActualLab.Fusion.EntityFramework.Redis.Operations;

public record RedisOperationLogChangeTrackingOptions<TDbContext> : DbOperationCompletionTrackingOptions
{
    public static RedisOperationLogChangeTrackingOptions<TDbContext> Default { get; set; } = new();

    public Func<DbShard, string> PubSubKeyFactory { get; init; } = DefaultPubSubKeyFactory;

    public static string DefaultPubSubKeyFactory(DbShard shard)
    {
        var shardSuffix = shard.IsNone ? "" : $".{shard}";
        return $"{typeof(TDbContext).GetName()}{shardSuffix}._Operations";
    }
}
