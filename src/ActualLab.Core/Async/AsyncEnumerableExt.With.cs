namespace ActualLab.Async;

public static partial class AsyncEnumerableExt
{
    // WithBuffer

    public static IAsyncEnumerable<T> WithBuffer<T>(
        this IAsyncEnumerable<T> source,
        int bufferSize,
        CancellationToken cancellationToken = default)
        => source.WithBuffer(bufferSize, allowSynchronousContinuations: true, cancellationToken);

    public static IAsyncEnumerable<T> WithBuffer<T>(
        this IAsyncEnumerable<T> source,
        int bufferSize,
        bool allowSynchronousContinuations,
        CancellationToken cancellationToken = default)
    {
        if (bufferSize < 1)
            return source;

        var buffer = source.ToChannel(new BoundedChannelOptions(bufferSize) {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = allowSynchronousContinuations,
        }, cancellationToken);
        return buffer.Reader.ReadAllAsync(cancellationToken);
    }

    public static IAsyncEnumerable<T> WithBuffer<T>(
        this IAsyncEnumerable<T> source,
        BoundedChannelOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.Capacity < 1)
            return source;

        var buffer = source.ToChannel(options, cancellationToken);
        return buffer.Reader.ReadAllAsync(cancellationToken);
    }

    // WithTimeout

    public static IAsyncEnumerable<T> WithItemTimeout<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan itemTimeout,
        CancellationToken cancellationToken = default)
        => source.WithItemTimeout(itemTimeout, itemTimeout, MomentClockSet.Default.CpuClock, cancellationToken);

    public static IAsyncEnumerable<T> WithItemTimeout<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan firstItemTimeout,
        TimeSpan itemTimeout,
        CancellationToken cancellationToken = default)
        => source.WithItemTimeout(firstItemTimeout, itemTimeout, MomentClockSet.Default.CpuClock, cancellationToken);

    public static IAsyncEnumerable<T> WithItemTimeout<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan itemTimeout,
        MomentClock clock,
        CancellationToken cancellationToken = default)
        => source.WithItemTimeout(itemTimeout, itemTimeout, clock, cancellationToken);

    public static async IAsyncEnumerable<T> WithItemTimeout<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan firstItemTimeout,
        TimeSpan itemTimeout,
        MomentClock clock,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ReSharper disable once NotDisposedResource
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        await using var _ = enumerator.ConfigureAwait(false);

        var nextTimeout = firstItemTimeout;
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable MA0040
            var hasMoreTask = enumerator.MoveNextAsync();
#pragma warning restore MA0040
            var hasMore = hasMoreTask.IsCompleted
                ? await hasMoreTask.ConfigureAwait(false)
                : await hasMoreTask.AsTask()
                    .WaitAsync(clock, nextTimeout, cancellationToken)
                    .ConfigureAwait(false);
            if (hasMore)
                yield return enumerator.Current;
            else
                yield break;
            nextTimeout = itemTimeout;
        }
    }
}
