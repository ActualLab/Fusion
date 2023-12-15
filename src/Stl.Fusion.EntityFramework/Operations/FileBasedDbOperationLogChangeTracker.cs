using Microsoft.EntityFrameworkCore;
using ActualLab.IO;
using ActualLab.Multitenancy;

namespace ActualLab.Fusion.EntityFramework.Operations;

public class FileBasedDbOperationLogChangeTracker<TDbContext>(
    FileBasedDbOperationLogChangeTrackingOptions<TDbContext> options,
    IServiceProvider services
    ) : DbOperationCompletionTrackerBase<TDbContext, FileBasedDbOperationLogChangeTrackingOptions<TDbContext>>(options, services)
    where TDbContext : DbContext
{
    protected override DbOperationCompletionTrackerBase.TenantWatcher CreateTenantWatcher(Symbol tenantId)
        => new TenantWatcher(this, tenantId);

    protected new class TenantWatcher : DbOperationCompletionTrackerBase.TenantWatcher
    {
        protected FileBasedDbOperationLogChangeTracker<TDbContext> Owner { get; }
        protected FilePath FilePath { get; }
        protected FileSystemWatcher Watcher { get; init; }
        protected IObservable<FileSystemEventArgs> Observable { get; init; }
        protected IDisposable Subscription { get; init; }

        public TenantWatcher(FileBasedDbOperationLogChangeTracker<TDbContext> owner, Symbol tenantId)
            : base(owner.TenantRegistry.Get(tenantId))
        {
            Owner = owner;
            FilePath = Owner.Options.FilePathFactory.Invoke(Tenant);
            Watcher = new FileSystemWatcher(FilePath.DirectoryPath, FilePath.FileName);
            Observable = Watcher.ToObservable();
            Subscription = Observable.Subscribe(_ => CompleteWaitForChanges());
            Watcher.EnableRaisingEvents = true;
        }
    }
}
