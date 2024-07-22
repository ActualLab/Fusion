using ActualLab.IO;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public record FileSystemDbLogWatcherOptions<TDbContext>
    where TDbContext : DbContext
{
    public static FileSystemDbLogWatcherOptions<TDbContext> Default { get; set; } = new();

    public Func<DbShard, Type, FilePath> FilePathFormatter { get; init; } = DefaultFilePathFormatter;

    public static FilePath DefaultFilePathFormatter(DbShard shard, Type dbEntryType)
    {
        var dbContextName = typeof(TDbContext).Name;
        var dbEntryName = dbEntryType.Name;
        var shardSuffix = shard.IsNone ? "" : $"_{shard}";
        var appTempDir = FilePath.GetApplicationTempDirectory("", true);
        return appTempDir & FilePath.GetHashedName($"{dbContextName}_{dbEntryName}{shardSuffix}.tracker");
    }
}
