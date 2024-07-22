using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Caching;

public enum RpcCacheInfoCaptureMode
{
    None = 0,
    KeyOnly = 1,
    KeyAndData = 3,
}

public sealed class RpcCacheInfoCapture
{
    public readonly RpcCacheInfoCaptureMode CaptureMode;
    public readonly RpcCacheEntry? CacheEntry;
    public RpcCacheKey? Key;
    public TaskCompletionSource<RpcCacheValue>? ValueSource; // Non-error IFF RpcOutboundCall.ResultTask is non-error

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
        if (captureMode == RpcCacheInfoCaptureMode.KeyAndData)
            ValueSource = new();
        CacheEntry = cacheEntry;
    }

    public bool HasKeyAndValue(out RpcCacheKey key, out TaskCompletionSource<RpcCacheValue> valueSource)
    {
        if (ReferenceEquals(Key, null) || ValueSource == null) {
            key = null!;
            valueSource = null!;
            return false;
        }

        key = Key;
        valueSource = ValueSource;
        return true;
    }

    public void CaptureKey(RpcOutboundContext context, RpcMessage? message)
        => Key ??= message is { Arguments: null } // This indicates ArgumentData is there
            ? new RpcCacheKey(context.MethodDef!.Service.Name, context.MethodDef.Name, message.ArgumentData)
            : null;

    public void CaptureValue(RpcMessage message)
    {
        var hash = message.Headers.TryGet(RpcHeaderNames.Hash, out var hashHeader)
            ? hashHeader.Value
            : "";
        ValueSource?.TrySetResult(new RpcCacheValue(message.ArgumentData, hash));
    }

    public void CaptureValue(RpcCacheValue value)
        => ValueSource?.TrySetResult(value);

    public void CaptureValue(Exception error)
        => ValueSource?.TrySetException(error);

    public void CaptureValue(CancellationToken cancellationToken)
        => ValueSource?.TrySetCanceled(cancellationToken);

    public void CaptureValue(bool isCancelled, Exception error, CancellationToken cancellationToken)
    {
        if (isCancelled)
            ValueSource?.TrySetCanceled(cancellationToken);
        else
            ValueSource?.TrySetException(error);
    }
}
