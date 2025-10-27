using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ActualLab.Channels;

public class UnbufferedPushSequence<T> : IAsyncEnumerable<T>, IDisposable
{
    private readonly SemaphoreSlim _itemAvailable = new SemaphoreSlim(0, 1);
    private readonly SemaphoreSlim _pushAllowed = new SemaphoreSlim(1, 1);
    private volatile TaskCompletionSource<T> _currentItem = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _isCompleted;
    private volatile Exception? _completionError;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    public async Task Push(T value, CancellationToken cancellationToken = default)
    {
        if (_isCompleted)
            throw new InvalidOperationException("Channel is closed.");

        await _pushAllowed.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isCompleted)
                throw new InvalidOperationException("Channel is closed.");

            lock (_lock)
            {
                // Set the result for the current waiting consumer
                _currentItem.TrySetResult(value);
                // Prepare next item TCS
                _currentItem = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            // Signal that an item is available
            _itemAvailable.Release();
        }
        catch
        {
            _pushAllowed.Release();
            throw;
        }
    }

    public async Task Complete(Exception? error = null)
    {
        lock (_lock)
        {
            if (_isCompleted)
                return;

            _isCompleted = true;
            _completionError = error;

            // Complete any pending push or enumeration
            if (error != null)
                _currentItem.TrySetException(error);
            else
                _currentItem.TrySetCanceled();
        }

        _pushAllowed.Release();
        _itemAvailable.Release();
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (_isCompleted)
            {
                if (_completionError != null)
                    throw _completionError;
                yield break;
            }

            await _itemAvailable.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (_isCompleted)
            {
                if (_completionError != null)
                    throw _completionError;
                yield break;
            }

            Task<T> itemTask;
            lock (_lock)
            {
                itemTask = _currentItem.Task;
            }

            try
            {
                yield return await itemTask.ConfigureAwait(false);
            }
            finally
            {
                _pushAllowed.Release();
            }
        }
    }

    public void Dispose()
    {
        Complete();
        _itemAvailable.Dispose();
        _pushAllowed.Dispose();
    }
}
