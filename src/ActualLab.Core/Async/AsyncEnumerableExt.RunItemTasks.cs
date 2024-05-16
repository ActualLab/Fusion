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
        var stoppingTasks = new List<Task>();
        var addedItems = new List<TItem>();
        var removedItems = new List<TItem>();
        try {
            await foreach (var items in itemSets.WithCancellation(cancellationToken).ConfigureAwait(false)) {
                // Stopping previously queued tasks
                await Task.WhenAll(stoppingTasks).SilentAwait(false);
                stoppingTasks.Clear();

#if NET5_0_OR_GREATER
                Update(items as IReadOnlySet<TItem> ?? items.ToHashSet());
#else
                Update(items.ToHashSet());
#endif
            }
        }
        finally {
            Update(new HashSet<TItem>());
            await Task.WhenAll(stoppingTasks).SilentAwait(false);
        }
        return;

#if NET5_0_OR_GREATER
        void Update(IReadOnlySet<TItem> items)
#else
        void Update(HashSet<TItem> items)
#endif
        {
            // Compute addedItems
            addedItems.Clear();
            foreach (var item in items)
                if (!runningTasks.ContainsKey(item))
                    addedItems.Add(item);

            // Compute removedItems
            removedItems.Clear();
            foreach (var item in runningTasks.Keys)
                if (!items.Contains(item))
                    removedItems.Add(item);

            // Invoke changeHandler
            changeHandler?.Invoke(items, addedItems, removedItems);

            // Starting new tasks
            foreach (var item in addedItems) {
                var stopTokenSource = new CancellationTokenSource();
                var task = itemTaskFactory.Invoke(item, stopTokenSource.Token);
                runningTasks.Add(item, (stopTokenSource, task));
            }

            // Stopping old tasks
            foreach (var item in removedItems) {
                var (stopTokenSource, task) = runningTasks[item];
                stoppingTasks.Add(task);
                runningTasks.Remove(item);
                stopTokenSource.CancelAndDisposeSilently();
            }
        }
    }
}
