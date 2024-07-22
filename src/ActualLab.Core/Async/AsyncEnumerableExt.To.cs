using ActualLab.Channels;

namespace ActualLab.Async;

public static partial class AsyncEnumerableExt
{
    private static readonly UnboundedChannelOptions DefaultUnboundedChannelOptions = new();

    // ToResults

    public static async IAsyncEnumerable<Result<T>> ToResults<T>(
        this IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ReSharper disable once NotDisposedResource
        var enumerator = source.GetAsyncEnumerator(cancellationToken);
        await using var _ = enumerator.ConfigureAwait(false);

        Result<T> item = default;
        while (true) {
            var hasMore = false;
            try {
                hasMore = await enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(false);
                if (hasMore)
                    item = enumerator.Current;
            }
            catch (Exception ex) when (!ex.IsCancellationOf(cancellationToken)) {
                item = new Result<T>(default!, ex);
            }

            if (item.HasError) {
                yield return item;
                yield break;
            }
            if (hasMore)
                yield return item;
            else
                yield break;
        }
    }

    // ToUnboundedChannel

    public static Channel<T> ToUnboundedChannel<T>(
        this IEnumerable<T> source,
        CancellationToken cancellationToken = default)
        => source.ToUnboundedChannel(DefaultUnboundedChannelOptions, cancellationToken);

    public static Channel<T> ToUnboundedChannel<T>(
        this IEnumerable<T> source,
        UnboundedChannelOptions options,
        CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<T>(options);
        _ = source.CopyTo(channel, ChannelCopyMode.CopyAllSilently, cancellationToken);
        return channel;
    }

    public static Channel<T> ToUnboundedChannel<T>(
        this IAsyncEnumerable<T> source,
        CancellationToken cancellationToken = default)
        => source.ToUnboundedChannel(DefaultUnboundedChannelOptions, cancellationToken);

    public static Channel<T> ToUnboundedChannel<T>(
        this IAsyncEnumerable<T> source,
        UnboundedChannelOptions options,
        CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<T>(options);
        _ = source.CopyTo(channel, ChannelCopyMode.CopyAllSilently, cancellationToken);
        return channel;
    }

    // ToBoundedChannel

    public static Channel<T> ToBoundedChannel<T>(
        this IEnumerable<T> source,
        BoundedChannelOptions options,
        CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<T>(options);
        _ = source.CopyTo(channel, ChannelCopyMode.CopyAllSilently, cancellationToken);
        return channel;
    }

    public static Channel<T> ToBoundedChannel<T>(
        this IAsyncEnumerable<T> source,
        BoundedChannelOptions options,
        CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<T>(options);
        _ = source.CopyTo(channel, ChannelCopyMode.CopyAllSilently, cancellationToken);
        return channel;
    }

    // CopyTo

    public static async Task CopyTo<T>(this IEnumerable<T> source,
        ChannelWriter<T> writer,
        ChannelCopyMode copyMode,
        CancellationToken cancellationToken = default)
    {
        try {
            foreach (var item in source)
                await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            if ((copyMode & ChannelCopyMode.CopyCompletion) != 0)
                writer.TryComplete();
        }
        catch (OperationCanceledException oce) {
            if ((copyMode & ChannelCopyMode.CopyCancellation) != 0)
                writer.TryComplete(oce);
            if ((copyMode & ChannelCopyMode.Silently) == 0)
                throw;
        }
        catch (Exception e) {
            if ((copyMode & ChannelCopyMode.CopyError) != 0)
                writer.TryComplete(e);
            if ((copyMode & ChannelCopyMode.Silently) == 0)
                throw;
        }
    }

    public static async Task CopyTo<T>(this IAsyncEnumerable<T> source,
        ChannelWriter<T> writer,
        ChannelCopyMode copyMode,
        CancellationToken cancellationToken = default)
    {
        try {
            await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
                await writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
            if ((copyMode & ChannelCopyMode.CopyCompletion) != 0)
                writer.TryComplete();
        }
        catch (OperationCanceledException oce) {
            if ((copyMode & ChannelCopyMode.CopyCancellation) != 0)
                writer.TryComplete(oce);
            if ((copyMode & ChannelCopyMode.Silently) == 0)
                throw;
        }
        catch (Exception e) {
            if ((copyMode & ChannelCopyMode.CopyError) != 0)
                writer.TryComplete(e);
            if ((copyMode & ChannelCopyMode.Silently) == 0)
                throw;
        }
    }
}
