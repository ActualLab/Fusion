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
    public RpcCacheKey? Key;
    public TaskCompletionSource<TextOrBytes>? DataSource; // Non-error IFF RpcOutboundCall.ResultTask is non-error

    public RpcCacheInfoCapture(RpcCacheInfoCaptureMode captureMode = RpcCacheInfoCaptureMode.KeyAndData)
    {
        if (captureMode == RpcCacheInfoCaptureMode.None)
            throw new ArgumentOutOfRangeException(nameof(captureMode));

        CaptureMode = captureMode;
        if (captureMode == RpcCacheInfoCaptureMode.KeyAndData)
            DataSource = new();
    }

    public bool HasKeyAndData(out RpcCacheKey key, out TaskCompletionSource<TextOrBytes> dataSource)
    {
        if (ReferenceEquals(Key, null) || DataSource == null) {
            key = null!;
            dataSource = null!;
            return false;
        }

        key = Key;
        dataSource = DataSource;
        return true;
    }

    public void CaptureKey(RpcOutboundContext context, RpcMessage? message)
        => Key ??= message is { Arguments: null } // This indicates ArgumentData is there
            ? new RpcCacheKey(context.MethodDef!.Service.Name, context.MethodDef.Name, message.ArgumentData)
            : null;

    public void CaptureData(RpcMessage message)
        => DataSource?.TrySetResult(message.ArgumentData);

    public void CaptureData(Exception error)
        => DataSource?.TrySetException(error);

    public void CaptureData(CancellationToken cancellationToken)
        => DataSource?.TrySetCanceled(cancellationToken);

    public void CaptureData(bool isCancelled, Exception error, CancellationToken cancellationToken)
    {
        if (isCancelled)
            DataSource?.TrySetCanceled(cancellationToken);
        else
            DataSource?.TrySetException(error);
    }
}
