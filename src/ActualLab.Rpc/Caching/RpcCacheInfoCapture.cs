using ActualLab.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Caching;

/// <summary>
/// Defines what cache-related information to capture during an RPC call.
/// </summary>
public enum RpcCacheInfoCaptureMode
{
    None = 0,
    KeyOnly = 1,
    KeyAndData = 3,
}

#pragma warning disable RCS1059

/// <summary>
/// Captures cache key and value information during outbound RPC calls for cache invalidation and reuse.
/// </summary>
public sealed class RpcCacheInfoCapture
{
    private readonly AsyncTaskMethodBuilder<Unit> _cacheFallbackSource = AsyncTaskMethodBuilderExt.New<Unit>();

    public readonly RpcCacheInfoCaptureMode CaptureMode;
    public readonly RpcCacheEntry? CacheEntry;
    public RpcOutboundCall? Call;
    public RpcCacheKey? Key;
    public object? ValueOrError; // Either RpcCacheValue or Exception
    public Task WhenCacheFallback => _cacheFallbackSource.Task;

    public RpcCacheInfoCapture(RpcCacheInfoCaptureMode captureMode)
        : this(cacheEntry: null, captureMode)
    { }

    public RpcCacheInfoCapture(
        RpcCacheEntry? cacheEntry = null,
        RpcCacheInfoCaptureMode captureMode = RpcCacheInfoCaptureMode.KeyAndData)
    {
        if (captureMode == RpcCacheInfoCaptureMode.None)
            throw new ArgumentOutOfRangeException(nameof(captureMode));

        CaptureMode = captureMode;
        CacheEntry = cacheEntry;
    }

    public bool TrySetCacheFallback()
        => _cacheFallbackSource.TrySetResult(default);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasKeyAndValue(
        [NotNullWhen(true)] out RpcCacheKey? key,
        [NotNullWhen(true)] out object? valueOrError)
    {
        // Call is set by CaptureKey, i.e. only once the call is sent. Invoke registers before it
        // sends, so a call can fail - connect timeout, reroute, peer stop - while this is still null.
        if (Volatile.Read(ref Call) is not { } call) {
            key = null;
            valueOrError = null;
            return false;
        }

        lock (call.Lock) {
            key = Key;
            valueOrError = ValueOrError;
            return key is not null && valueOrError is not null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RequireKeyAndValue(out RpcCacheKey key, out object valueOrError)
    {
        if (!HasKeyAndValue(out key!, out valueOrError!))
            throw Errors.InternalError(
                $"{nameof(RequireKeyAndValue)} is called, but CaptureMode is {CaptureMode}, "
                + $"Call is {Call}, and Key is {Key}.");
    }

    public void CaptureKey(RpcOutboundContext context, RpcOutboundMessage message)
    {
        var call = context.Call;
        lock (call!) {
            // Both fields are read lock-free elsewhere, hence the release stores
            Volatile.Write(ref Call, call);
            // Outbound serialization copies small payloads and detaches large buffers from reuse,
            // so the retained memory stays immutable without another copy here.
            if (Key is null)
                Volatile.Write(ref Key, new RpcCacheKey(context.MethodDef!.FullName, message.ArgumentData));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CaptureValueFromLock(RpcInboundMessage message)
    {
        if (CaptureMode == RpcCacheInfoCaptureMode.KeyAndData)
            // Must copy ArgumentData because the buffer may be reused after this call
            ValueOrError = new RpcCacheValue(
                message.ArgumentData.ToArray(),
                message.Headers.TryGet(WellKnownRpcHeaders.Hash) ?? "");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CaptureValueFromLock(RpcCacheValue value)
    {
        if (CaptureMode == RpcCacheInfoCaptureMode.KeyAndData)
            // ReSharper disable once InconsistentlySynchronizedField
            ValueOrError = value;
    }

    public void CaptureErrorFromLock(bool isCancelled, Exception error, CancellationToken cancellationToken)
    {
        if (isCancelled)
            CaptureCancellationFromLock(cancellationToken);
        else
            CaptureErrorFromLock(error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CaptureErrorFromLock(Exception error)
    {
        if (CaptureMode == RpcCacheInfoCaptureMode.KeyAndData)
            // ReSharper disable once InconsistentlySynchronizedField
            ValueOrError ??= error;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CaptureCancellationFromLock(CancellationToken cancellationToken)
    {
        if (CaptureMode == RpcCacheInfoCaptureMode.KeyAndData)
            // ReSharper disable once InconsistentlySynchronizedField
            ValueOrError ??= new OperationCanceledException(cancellationToken);
    }
}
