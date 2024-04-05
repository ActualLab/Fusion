namespace ActualLab.Async;

public static partial class AsyncEnumerableExt
{
    // RunItemTasks

    public static Task RunItemTasks<TItem>(
        this IAsyncEnumerable<IEnumerable<TItem>> itemSets,
        Func<TItem, CancellationToken, Task> itemTaskFactory,
        CancellationToken cancellationToken = default)
        where TItem : notnull
        => itemSets.RunItemTasks(itemTaskFactory, null, cancellationToken);

    public static async Task RunItemTasks<TItem>(
        this IAsyncEnumerable<IEnumerable<TItem>> itemSets,
        Func<TItem, CancellationToken, Task> itemTaskFactory,
#if NET5_0_OR_GREATER
        Action<IReadOnlySet<TItem>, List<TItem>, List<TItem>>? changeHandler,
#else
        Action<HashSet<TItem>, List<TItem>, List<TItem>>? changeHandler,
#endif
        CancellationToken cancellationToken = default)
        where TItem : notnull
    {
        var runningTasks = new Dictionary<TItem, (CancellationTokenSource StopTokenSource, Task Task)>();
        var stoppingTasks = new HashSet<Task>();
        var addedItems = new List<TItem>();
        var removedItems = new List<TItem>();
        var @lock = stoppingTasks;
        try {
            await foreach (var itemSet in itemSets.ConfigureAwait(false).WithCancellation(cancellationToken)) {
#if NET5_0_OR_GREATER
                var activeItems = itemSet as IReadOnlySet<TItem> ?? itemSet.ToHashSet();
#else
                var activeItems = itemSet.ToHashSet();
#endif
                lock (@lock) {
                    foreach (var item in activeItems)
                        if (!runningTasks.ContainsKey(item))
                            addedItems.Add(item);
                    foreach (var item in runningTasks.Keys)
                        if (!activeItems.Contains(item))
                            removedItems.Add(item);
                    changeHandler?.Invoke(activeItems, addedItems, removedItems);

                    // Starting new tasks
                    foreach (var item in addedItems) {
                        var stopTokenSource = new CancellationTokenSource();
                        var task = itemTaskFactory.Invoke(item, stopTokenSource.Token);
                        runningTasks.Add(item, (stopTokenSource, task));
                        _ = task.ContinueWith(t => {
                                lock (@lock)
                                    stoppingTasks.Remove(t); // Must be inside the lock
                            },
                            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    }

                    // Stopping old tasks
                    foreach (var item in removedItems) {
                        var (stopTokenSource, task) = runningTasks[item];
                        stoppingTasks.Add(task);
                        runningTasks.Remove(item);
                        stopTokenSource.CancelAndDisposeSilently();
                    }
                }

                // Clean up temp lists
                addedItems.Clear();
                removedItems.Clear();
            }
        }
        finally {
            // Terminating
            Task[] lastStoppingTasks;
            lock (@lock) {
                foreach (var (_, (stopTokenSource, task)) in runningTasks) {
                    stoppingTasks.Add(task);
                    stopTokenSource.CancelAndDisposeSilently();
                }
                lastStoppingTasks = stoppingTasks.ToArray();
            }
            await Task.WhenAll(lastStoppingTasks).ConfigureAwait(false);
        }
    }
}
