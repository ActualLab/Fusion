using System.Runtime.ExceptionServices;

namespace ActualLab.Async;

public static partial class AsyncEnumerableExt
{
    // SkipNullItems

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<T> SkipNullItems<T>(this IAsyncEnumerable<T?> source)
        where T : class
        => source.Where(x => x is not null)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<T> SkipNullItems<T>(this IAsyncEnumerable<T?> source)
        where T : struct
        => source.Where(x => x is not null).Select(x => x!.Value);

    // SkipSyncItems

    public static IAsyncEnumerable<T> SkipSyncItems<T>(
        this IAsyncEnumerable<T> items,
        CancellationToken cancellationToken = default)
        => items.SkipSyncItems(false, cancellationToken);

    public static async IAsyncEnumerable<T> SkipSyncItems<T>(
        this IAsyncEnumerable<T> items,
        bool alwaysYieldFirstItem,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ReSharper disable once NotDisposedResource
        var enumerator = items.GetAsyncEnumerator(cancellationToken);
        await using var _1 = enumerator.ConfigureAwait(false);

        var last = default(T);
        var hasLast = false;
        var error = (ExceptionDispatchInfo?)null;
        while (true) {
            ValueTask<bool> hasNextTask;
            try {
                hasNextTask = enumerator.MoveNextAsync();
            }
            catch (Exception e) {
                error = ExceptionDispatchInfo.Capture(e);
                break;
            }

            if (hasLast && (alwaysYieldFirstItem || !hasNextTask.IsCompleted)) {
                alwaysYieldFirstItem = hasLast = false;
                yield return last!;
            }

            try {
                if (!await hasNextTask.ConfigureAwait(false))
                    break;
            }
            catch (Exception e) {
                error = ExceptionDispatchInfo.Capture(e);
                break;
            }

            last = enumerator.Current;
            hasLast = true;
        }
        if (hasLast)
            yield return last!;
        error?.Throw();
    }

    // SuppressXxx

    public static async IAsyncEnumerable<T> SuppressExceptions<T>(
        this IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ReSharper disable once NotDisposedResource
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        await using var _ = enumerator.ConfigureAwait(false);

        while (true) {
            bool hasMore;
            T item = default!;
            try {
                cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable MA0040
                hasMore = await enumerator.MoveNextAsync().ConfigureAwait(false);
#pragma warning restore MA0040
                if (hasMore)
                    item = enumerator.Current;
            }
            catch (Exception) {
                yield break;
            }
            if (hasMore)
                yield return item;
            else
                yield break;
        }
    }

    public static async IAsyncEnumerable<T> SuppressCancellation<T>(
        this IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ReSharper disable once NotDisposedResource
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        await using var _ = enumerator.ConfigureAwait(false);

        while (true) {
            bool hasMore;
            T item = default!;
            try {
                cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable MA0040
                hasMore = await enumerator.MoveNextAsync().ConfigureAwait(false);
#pragma warning restore MA0040
                if (hasMore)
                    item = enumerator.Current;
            }
            catch (OperationCanceledException) {
                yield break;
            }
            if (hasMore)
                yield return item;
            else
                yield break;
        }
    }

    public static async IAsyncEnumerable<T> EnforceCancellation<T>(
        this IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        try {
            while (await enumerator.MoveNextAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false))
                yield return enumerator.Current;
        }
        finally {
            await enumerator.DisposeAsync().SilentAwait(false);
        }
    }
}
