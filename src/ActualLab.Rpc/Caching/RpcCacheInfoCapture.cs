using ActualLab.Internal;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Caching;

public enum RpcCacheInfoCaptureMode
{
    None = 0,
    KeyOnly = 1,
    KeyAndData = 3,
}

#pragma warning disable RCS1059

public sealed class RpcCacheInfoCapture
{
    public readonly RpcCacheInfoCaptureMode CaptureMode;
    public readonly RpcCacheEntry? CacheEntry;
    public volatile RpcOutboundCall? Call;
    public volatile RpcCacheKey? Key;
    public object? ValueOrError; // Either RpcCacheValue or Exception

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasKeyAndValue(
        [NotNullWhen(true)] out RpcCacheKey? key,
        [NotNullWhen(true)] out object? valueOrError)
    {
        lock (Call!.Lock) {
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
                $"{nameof(RequireKeyAndValue)} is called, but CaptureMode is {CaptureMode} and Key is {Key}.");
    }

    public void CaptureKey(RpcOutboundContext context, RpcMessage message)
    {
        var call = context.Call;
        lock (call!) {
            Call = call;
            // ReSharper disable once NonAtomicCompoundOperator
            Key ??= new RpcCacheKey(context.MethodDef!.FullName, message.ArgumentData);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CaptureValueFromLock(RpcMessage message)
    {
        if (CaptureMode == RpcCacheInfoCaptureMode.KeyAndData)
            // ReSharper disable once InconsistentlySynchronizedField
            ValueOrError = new RpcCacheValue(
                message.ArgumentData,
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
