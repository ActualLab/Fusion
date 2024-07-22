using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Npgsql;

public record NpgsqlDbLogWatcherOptions<TDbContext>
    where TDbContext : DbContext
{
    public static NpgsqlDbLogWatcherOptions<TDbContext> Default { get; set; } = new();

    public Func<DbShard, Type, string> ChannelNameFormatter { get; init; } = DefaultChannelNameFormatter;
    public RetryDelaySeq TrackerRetryDelays { get; init; } = RetryDelaySeq.Exp(1, 10);

    public static string DefaultChannelNameFormatter(DbShard shard, Type dbEntryType)
    {
        var dbContextName = typeof(TDbContext).Name;
        var dbEntryName = dbEntryType.Name;
        var shardSuffix = shard.IsNone ? "" : $"_{shard}";
        return $"{dbContextName}_{dbEntryName}{shardSuffix}";
    }
}
