using ActualLab.IO;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

/// <summary>
/// Configuration options for <see cref="FileSystemDbLogWatcher{TDbContext, TDbEntry}"/>,
/// including the tracker file path formatter.
/// </summary>
public record FileSystemDbLogWatcherOptions<TDbContext>
    where TDbContext : DbContext
{
    public static FileSystemDbLogWatcherOptions<TDbContext> Default { get; set; } = new();

    public Func<string, Type, FilePath> FilePathFormatter { get; init; } = DefaultFilePathFormatter;

    public static FilePath DefaultFilePathFormatter(string shard, Type dbEntryType)
    {
        var dbContextName = typeof(TDbContext).Name;
        var dbEntryName = dbEntryType.Name;
        var shardSuffix = DbShard.IsSingle(shard) ? "" : $"_{shard}";
        var appTempDir = FilePath.GetApplicationTempDirectory("", true);
        return appTempDir & (FilePath.GetHashedName($"{dbContextName}_{dbEntryName}{shardSuffix}") + ".tracker");
    }
}
