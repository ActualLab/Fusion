using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.IO;
using Microsoft.EntityFrameworkCore;
using FileSystemWatcher = System.IO.FileSystemWatcher;

namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public class FileSystemDbLogWatcher<TDbContext, TDbEntry>(
    FileSystemDbLogWatcherOptions<TDbContext> settings,
    IServiceProvider services
    ) : DbLogWatcher<TDbContext, TDbEntry>(services)
    where TDbContext : DbContext
{
    public FileSystemDbLogWatcherOptions<TDbContext> Settings { get; } = settings;

    protected override DbShardWatcher CreateShardWatcher(string shard)
        => new ShardWatcher(this, shard);

    // Nested types

    protected class ShardWatcher : DbShardWatcher
    {
        public FileSystemDbLogWatcher<TDbContext, TDbEntry> Owner { get; }
        public FilePath FilePath { get; }
        public FileSystemWatcher Watcher { get; init; }
        public IObservable<FileSystemEventArgs> Observable { get; init; }
        public IDisposable Subscription { get; init; }

        public ShardWatcher(FileSystemDbLogWatcher<TDbContext, TDbEntry> owner, string shard) : base(shard)
        {
            Owner = owner;
            FilePath = Owner.Settings.FilePathFormatter.Invoke(Shard, typeof(TDbEntry));
            Watcher = new FileSystemWatcher(FilePath.DirectoryPath, FilePath.FileName);
            Observable = Watcher.ToObservable();
            Subscription = Observable.Subscribe(_ => MarkChanged());
            Watcher.EnableRaisingEvents = true;
        }

        protected override Task DisposeAsyncCore()
        {
            Subscription.Dispose();
            return Task.CompletedTask;
        }

        public override Task NotifyChanged(CancellationToken cancellationToken)
        {
#pragma warning disable MA0042
            var filePath = FilePath.Value;
            if (!File.Exists(filePath))
                File.WriteAllText(filePath, ""); // We do it just once, so fine to use sync version
            else
                File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);
            return Task.CompletedTask;
#pragma warning restore MA0042
        }
    }
}
