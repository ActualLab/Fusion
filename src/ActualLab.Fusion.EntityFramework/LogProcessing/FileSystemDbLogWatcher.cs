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
    where TDbEntry : class, ILogEntry
{
    public FileSystemDbLogWatcherOptions<TDbContext> Settings { get; } = settings;

    public override Task Notify(DbShard shard, CancellationToken cancellationToken = default)
    {
        if (StopToken.IsCancellationRequested)
            return Task.CompletedTask;

        var filePath = Settings.FilePathFormatter.Invoke(shard, typeof(TDbEntry));
        if (!File.Exists(filePath))
            File.WriteAllText(filePath, "");
        else
            File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow);
        return Task.CompletedTask;
    }

    protected override DbShardWatcher CreateShardWatcher(DbShard shard)
        => new ShardWatcher(this, shard);

    // Nested types

    protected class ShardWatcher : DbShardWatcher
    {
        protected FileSystemDbLogWatcher<TDbContext, TDbEntry> Owner { get; }
        protected FilePath FilePath { get; }
        protected FileSystemWatcher Watcher { get; init; }
        protected IObservable<FileSystemEventArgs> Observable { get; init; }
        protected IDisposable Subscription { get; init; }

        public ShardWatcher(FileSystemDbLogWatcher<TDbContext, TDbEntry> owner, DbShard shard) : base(shard)
        {
            Owner = owner;
            FilePath = Owner.Settings.FilePathFormatter.Invoke(Shard, typeof(TDbEntry));
            Watcher = new FileSystemWatcher(FilePath.DirectoryPath, FilePath.FileName);
            Observable = Watcher.ToObservable();
            Subscription = Observable.Subscribe(_ => MarkChanged());
            Watcher.EnableRaisingEvents = true;
        }
    }
}
