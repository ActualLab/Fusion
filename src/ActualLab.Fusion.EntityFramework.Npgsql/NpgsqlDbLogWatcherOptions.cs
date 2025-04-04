using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Npgsql;

public record NpgsqlDbLogWatcherOptions<TDbContext>
    where TDbContext : DbContext
{
    public static NpgsqlDbLogWatcherOptions<TDbContext> Default { get; set; } = new();

    public Func<string, Type, string> ChannelNameFormatter { get; init; } = DefaultChannelNameFormatter;
    public RetryDelaySeq TrackerRetryDelays { get; init; } = RetryDelaySeq.Exp(1, 10);

    public static string DefaultChannelNameFormatter(string shard, Type dbEntryType)
    {
        var dbContextName = typeof(TDbContext).Name;
        var dbEntryName = dbEntryType.Name;
        var shardSuffix = DbShard.IsSingle(shard) ? "" : $"_{shard}";
        return $"{dbContextName}_{dbEntryName}{shardSuffix}";
    }
}
