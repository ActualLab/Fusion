using ActualLab.IO;

namespace ActualLab.Fusion.EntityFramework.Operations;

public record FileBasedDbOperationLogChangeTrackingOptions<TDbContext> : DbOperationCompletionTrackingOptions
{
    public static FileBasedDbOperationLogChangeTrackingOptions<TDbContext> Default { get; set; } = new();

    public Func<DbShard, FilePath> FilePathFactory { get; init; } = DefaultFilePathFactory;

    public static FilePath DefaultFilePathFactory(DbShard shard)
    {
        var tDbContext = typeof(TDbContext);
        var appTempDir = FilePath.GetApplicationTempDirectory("", true);
        var shardSuffix = shard.IsNone ? "" : $"_{shard}";
        return appTempDir & FilePath.GetHashedName($"{tDbContext.Name}_{tDbContext.Namespace}{shardSuffix}.tracker");
    }
}
