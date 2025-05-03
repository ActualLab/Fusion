using System.Diagnostics.CodeAnalysis;
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
    public volatile RpcCacheKey? Key;
    public volatile object? ValueOrError; // Either RpcCacheValue or Exception

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
        key = Key;
        valueOrError = ValueOrError;
        return key is not null && valueOrError is not null;
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
        if (Key is null)
            lock (this)
                // ReSharper disable once NonAtomicCompoundOperator
                Key ??= new RpcCacheKey(context.MethodDef!.FullName, message.ArgumentData);
    }

    public void CaptureValue(RpcMessage message)
    {
        if (CaptureMode == RpcCacheInfoCaptureMode.KeyAndData && ValueOrError is null)
            lock (this)
                // ReSharper disable once NonAtomicCompoundOperator
                ValueOrError ??= new RpcCacheValue(
                    message.ArgumentData,
                    message.Headers.TryGet(WellKnownRpcHeaders.Hash) ?? "");
    }

    public void CaptureValue(RpcCacheValue value)
    {
        if (CaptureMode == RpcCacheInfoCaptureMode.KeyAndData && ValueOrError is null)
            lock (this)
                // ReSharper disable once NonAtomicCompoundOperator
                ValueOrError ??= value;
    }

    public void CaptureError(bool isCancelled, Exception error, CancellationToken cancellationToken)
    {
        if (isCancelled)
            CaptureCancellation(cancellationToken);
        else
            CaptureError(error);
    }

    public void CaptureError(Exception error)
    {
        if (CaptureMode == RpcCacheInfoCaptureMode.KeyAndData && ValueOrError is null)
            lock (this)
                // ReSharper disable once NonAtomicCompoundOperator
                ValueOrError ??= error;
    }

    public void CaptureCancellation(CancellationToken cancellationToken)
    {
        if (CaptureMode == RpcCacheInfoCaptureMode.KeyAndData && ValueOrError == null)
            lock (this)
                // ReSharper disable once NonAtomicCompoundOperator
                ValueOrError ??= new OperationCanceledException(cancellationToken);
    }
}
