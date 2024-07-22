namespace ActualLab.Rpc.Infrastructure;

public static class RpcHeadersExt
{
    public static readonly RpcHeader[] Empty = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcHeader[] OrEmpty(this RpcHeader[]? headers)
        => headers ?? Empty;

    public static bool TryGet(this RpcHeader[]? headers, string name, out RpcHeader header)
    {
        if (headers == null || headers.Length == 0) {
            header = default;
            return false;
        }

        foreach (var h in headers)
            if (StringComparer.Ordinal.Equals(h.Name, name)) {
                header = h;
                return true;
            }

        header = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcHeader[] WithUnlessExists(this RpcHeader[]? headers, string name, string value)
        => headers.WithUnlessExists(new RpcHeader(name, value));

    public static RpcHeader[] WithUnlessExists(this RpcHeader[]? headers, RpcHeader header)
    {
        if (headers == null || headers.Length == 0)
            return [header];

        if (headers.TryGet(header.Name, out _))
            return headers;

        var newHeaders = new RpcHeader[headers.Length + 1];
        headers.CopyTo(newHeaders, 0);
        newHeaders[^1] = header;
        return newHeaders;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcHeader[] With(this RpcHeader[]? headers, string name, string value)
        => headers.With(new RpcHeader(name, value));

    public static RpcHeader[] With(this RpcHeader[]? headers, RpcHeader header)
    {
        if (headers == null || headers.Length == 0)
            return [header];

        var newHeaders = new RpcHeader[headers.Length + 1];
        headers.CopyTo(newHeaders, 0);
        newHeaders[^1] = header;
        return newHeaders;
    }
}
