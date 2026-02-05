using System.Threading.Channels;
using ActualLab.Async;

namespace ActualLab.Channels;

/// <summary>
/// An unbuffered push-based async sequence that allows a single enumerator and synchronous push.
/// </summary>
public class UnbufferedPushSequence<T> : IAsyncEnumerable<T>, IAsyncDisposable
{
    // Push is disallowed until the enumeration started
    private readonly SemaphoreSlim _pushAllowed = new(0, 1);
#if USE_UNSAFE_ACCESSORS
    private volatile Task<T> _item = AsyncTaskMethodBuilderExt.New<T>().Task;
#else
    private volatile TaskCompletionSource<T> _item = TaskCompletionSourceExt.New<T>();
#endif
    private int _isCompleted;
    private int _enumeratorCount;

    public ValueTask DisposeAsync()
        => Complete();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask Push(T value, CancellationToken cancellationToken = default)
        => Push(new Result<T>(value), cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask Complete(CancellationToken cancellationToken = default)
        => Push(new Result<T>(default!, new ChannelClosedException()), cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask Complete(Exception? error, CancellationToken cancellationToken = default)
        => Push(new Result<T>(default!, error ?? new ChannelClosedException()), cancellationToken);

    public async ValueTask Push(Result<T> result, CancellationToken cancellationToken = default)
    {
        if (result.Error is ChannelClosedException e) {
            // A special case indicating closure
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
                return;

            GetItemSource(_item).TrySetException(e);
            try {
                _pushAllowed.Release();
            }
            catch (ObjectDisposedException) {
                // Intended
            }
            return;
        }

        if (_isCompleted != 0)
            throw new ChannelClosedException();

        try {
            await _pushAllowed.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (_isCompleted != 0)
                throw new ChannelClosedException();

#if USE_UNSAFE_ACCESSORS
            var oldItem = _item;
            var newItem = AsyncTaskMethodBuilderExt.New<T>().Task;
#else
            var oldItem = _item;
            var newItem = TaskCompletionSourceExt.New<T>();
#endif
            if (Interlocked.CompareExchange(ref _item, newItem, oldItem) != oldItem)
                // CAS failed, this should never happen due to _pushAllowed being held
                throw new InvalidOperationException("Concurrent modification detected.");

            GetItemSource(oldItem).TrySetFromResult(result);
        }
        catch (ObjectDisposedException) {
            // _pushAllowed is disposed, which indicates channel closure
            throw new ChannelClosedException();
        }
        catch {
            try {
                _pushAllowed.Release();
            }
            catch (ObjectDisposedException) {
                // Intended
            }
            throw;
        }
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _enumeratorCount) != 1)
            throw new InvalidOperationException("This type allows just a single enumeration.");

        try {
            if (!cancellationToken.CanBeCanceled) {
                while (true) {
                    var item = GetItemTask(_item);
                    _pushAllowed.Release();
                    var result = await item.ResultAwait(false);
                    if (result.Error is ChannelClosedException)
                        yield break;

                    yield return result.Value;
                }
            }

            while (true) {
                var item = GetItemTask(_item);
                _pushAllowed.Release();
                var result = await item.WaitAsync(cancellationToken).ResultAwait(false);
                if (result.Error is ChannelClosedException)
                    yield break;

                yield return result.Value;
            }
        }
        finally {
            _pushAllowed.Dispose();
        }
    }

    // Private methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if USE_UNSAFE_ACCESSORS
    private static Task<T> GetItemTask(Task<T> item)
        => item;
#else
    private static Task<T> GetItemTask(TaskCompletionSource<T> item)
        => item.Task;
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if USE_UNSAFE_ACCESSORS
    private static AsyncTaskMethodBuilder<T> GetItemSource(Task<T> item)
        => AsyncTaskMethodBuilderExt.FromTask(item);
#else
    private static TaskCompletionSource<T> GetItemSource(TaskCompletionSource<T> item)
        => item;
#endif
}
