namespace ActualLab.Rpc.Caching;

public enum RpcCacheInfoCaptureMode
{
    KeyAndData = 0,
    KeyOnly,
}

public sealed class RpcCacheInfoCapture
{
    public readonly RpcCacheInfoCaptureMode CaptureMode;
    public RpcCacheKey? Key;
    public TaskCompletionSource<TextOrBytes>? DataSource; // Non-error IFF RpcOutboundCall.ResultTask is non-error

    public RpcCacheInfoCapture(RpcCacheInfoCaptureMode captureMode = default)
    {
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

    public async ValueTask<(RpcCacheKey? Key, TextOrBytes? Data)> GetKeyAndData()
    {
        if (!HasKeyAndData(out var key, out var dataSource))
            return (null, null);

        var dataResult = await dataSource.Task.ResultAwait(false);
        return (key, dataResult.IsValue(out var data) ? (TextOrBytes?)data : null);
    }
}
