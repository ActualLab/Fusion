using System.Reactive.Linq;

namespace ActualLab.IO;

public static class FileSystemWatcherExt
{
    public static IObservable<FileSystemEventArgs> ToObservable(this FileSystemWatcher watcher)
    {
        var o1 = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                handler => {
                    watcher.Changed += handler;
                    watcher.Created += handler;
                    watcher.Deleted += handler;
                },
                handler => {
                    watcher.Changed -= handler;
                    watcher.Created -= handler;
                    watcher.Deleted -= handler;
                })
            .Select(p => p.EventArgs);
        var o2 = Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
                handler => {
                    watcher.Renamed += handler;
                },
                handler => {
                    watcher.Renamed -= handler;
                })
            .Select(p => p.EventArgs);
        return o1.Merge(o2).Publish().RefCount();
    }

    public static async Task<FileSystemEventArgs> GetFirstEvent(
        this FileSystemWatcher watcher,
        CancellationToken cancellationToken = default)
    {
        var tcs = AsyncTaskMethodBuilderExt.New<FileSystemEventArgs>(runContinuationsAsynchronously: false);
        var handler = (FileSystemEventHandler) ((_, args) => tcs.TrySetResult(args));
        try {
            watcher.Changed += handler;
            watcher.Created += handler;
            watcher.Deleted += handler;
            return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally {
            watcher.Changed -= handler;
            watcher.Created -= handler;
            watcher.Deleted -= handler;
        }
    }
}
