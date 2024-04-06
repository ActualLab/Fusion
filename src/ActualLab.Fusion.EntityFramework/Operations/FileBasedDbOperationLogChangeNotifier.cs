using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class FileBasedDbOperationLogChangeNotifier<TDbContext>(
    FileBasedDbOperationLogChangeTrackingOptions<TDbContext> options,
    IServiceProvider services
    ) : DbOperationCompletionNotifierBase<TDbContext, FileBasedDbOperationLogChangeTrackingOptions<TDbContext>>(options, services)
    where TDbContext : DbContext
{
    protected override Task Notify(DbShard shard)
    {
        var filePath = Options.FilePathFactory(shard);
        if (!File.Exists(filePath))
            File.WriteAllText(filePath, "");
        else
            File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);
        return Task.CompletedTask;
    }
}
