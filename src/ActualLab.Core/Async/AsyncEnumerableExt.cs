using System.Runtime.ExceptionServices;

namespace ActualLab.Async;

/// <summary>
/// Extension methods for <see cref="IAsyncEnumerable{T}"/>.
/// </summary>
public static partial class AsyncEnumerableExt
{
    // SkipNullItems

    extension<T>(IAsyncEnumerable<T?> source)
        where T : class
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncEnumerable<T> SkipNullItems()
            => SkipNullReferences(source ?? throw new ArgumentNullException(nameof(source)));
    }

    extension<T>(IAsyncEnumerable<T?> source)
        where T : struct
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncEnumerable<T> SkipNullItems()
            => SkipNullValues(source ?? throw new ArgumentNullException(nameof(source)));
    }

    extension<T>(IAsyncEnumerable<T> source)
    {
        // SkipSyncItems

        public IAsyncEnumerable<T> SkipSyncItems(CancellationToken cancellationToken = default)
            => source.SkipSyncItems(false, cancellationToken);

        public async IAsyncEnumerable<T> SkipSyncItems(
            bool alwaysYieldFirstItem,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // ReSharper disable once NotDisposedResource
            var enumerator = source.GetAsyncEnumerator(cancellationToken);
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

        public async IAsyncEnumerable<T> SuppressExceptions(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // ReSharper disable once NotDisposedResource
            var enumerator = source.GetAsyncEnumerator(cancellationToken);
            await using var _ = enumerator.ConfigureAwait(false);

            while (true) {
                bool hasMore;
                T item = default!;
                try {
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

        public async IAsyncEnumerable<T> SuppressCancellation(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // ReSharper disable once NotDisposedResource
            var enumerator = source.GetAsyncEnumerator(cancellationToken);
            await using var _ = enumerator.ConfigureAwait(false);

            while (true) {
                bool hasMore;
                T item = default!;
                try {
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
    }

    // Private methods

    private static async IAsyncEnumerable<T> SkipNullReferences<T>(
        IAsyncEnumerable<T?> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            if (item is not null)
                yield return item;
    }

    private static async IAsyncEnumerable<T> SkipNullValues<T>(
        IAsyncEnumerable<T?> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : struct
    {
        cancellationToken.ThrowIfCancellationRequested();
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            if (item.HasValue)
                yield return item.GetValueOrDefault();
    }
}
