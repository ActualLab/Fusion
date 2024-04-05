using Microsoft.EntityFrameworkCore;
using ActualLab.IO;
using FileSystemWatcher = System.IO.FileSystemWatcher;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class FileBasedDbOperationLogChangeTracker<TDbContext>(
    FileBasedDbOperationLogChangeTrackingOptions<TDbContext> options,
    IServiceProvider services
    ) : DbOperationCompletionTrackerBase<TDbContext, FileBasedDbOperationLogChangeTrackingOptions<TDbContext>>(options, services)
    where TDbContext : DbContext
{
    protected override DbShardWatcher CreateShardWatcher(DbShard shard)
        => new ShardWatcher(this, shard);

    // Nested types

    protected class ShardWatcher : DbShardWatcher
    {
        protected FileBasedDbOperationLogChangeTracker<TDbContext> Owner { get; }
        protected FilePath FilePath { get; }
        protected FileSystemWatcher Watcher { get; init; }
        protected IObservable<FileSystemEventArgs> Observable { get; init; }
        protected IDisposable Subscription { get; init; }

        public ShardWatcher(FileBasedDbOperationLogChangeTracker<TDbContext> owner, DbShard shard) : base(shard)
        {
            Owner = owner;
            FilePath = Owner.Options.FilePathFactory.Invoke(Shard);
            Watcher = new FileSystemWatcher(FilePath.DirectoryPath, FilePath.FileName);
            Observable = Watcher.ToObservable();
            Subscription = Observable.Subscribe(_ => CompleteWaitForChanges());
            Watcher.EnableRaisingEvents = true;
        }
    }
}
