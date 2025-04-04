using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Redis;

public record RedisDbLogWatcherOptions<TDbContext>
    where TDbContext : DbContext
{
    public static RedisDbLogWatcherOptions<TDbContext> Default { get; set; } = new();

    public Func<string, Type, string> PubSubKeyFormatter { get; init; } = DefaultPubSubKeyFormatter;
    public RetryDelaySeq WatchRetryDelays { get; init; } = RetryDelaySeq.Exp(1, 10);

    public static string DefaultPubSubKeyFormatter(string shard, Type dbEntryType)
    {
        var dbContextName = typeof(TDbContext).Name;
        var dbEntryName = dbEntryType.Name;
        var shardSuffix = DbShard.IsSingle(shard) ? "" : $".{shard}";
        return $"{dbContextName}.{dbEntryName}{shardSuffix}";
    }
}
