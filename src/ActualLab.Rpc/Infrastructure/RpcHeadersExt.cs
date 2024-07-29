namespace ActualLab.Rpc.Infrastructure;

public static class RpcHeadersExt
{
    public static readonly RpcHeader[] Empty = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcHeader[] OrEmpty(this RpcHeader[]? headers)
        => headers ?? Empty;

    public static string? TryGet(this RpcHeader[]? headers, string name)
    {
        if (headers == null || headers.Length == 0)
            return null;

        foreach (var h in headers)
            if (string.Equals(h.Name, name, StringComparison.Ordinal))
                return h.Value;

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcHeader[] WithUnlessExists(this RpcHeader[]? headers, string name, string value)
        => headers.WithUnlessExists(new RpcHeader(name, value));

    public static RpcHeader[] WithUnlessExists(this RpcHeader[]? headers, RpcHeader header)
    {
        if (headers == null || headers.Length == 0)
            return [header];

        if (headers.TryGet(header.Name) != null)
            return headers;

        var newHeaders = new RpcHeader[headers.Length + 1];
        headers.CopyTo(newHeaders, 0);
        newHeaders[^1] = header;
        return newHeaders;
    }

    public static RpcHeader[] With(this RpcHeader[]? headers, RpcHeader header)
    {
        if (headers == null || headers.Length == 0)
            return [header];

        var newHeaders = new RpcHeader[headers.Length + 1];
        headers.CopyTo(newHeaders, 0);
        newHeaders[^1] = header;
        return newHeaders;
    }

    public static RpcHeader[] With(this RpcHeader[]? headers, RpcHeader header1, RpcHeader header2)
    {
        if (headers == null || headers.Length == 0)
            return [header1, header2];

        var newHeaders = new RpcHeader[headers.Length + 2];
        headers.CopyTo(newHeaders, 0);
        newHeaders[^2] = header1;
        newHeaders[^1] = header2;
        return newHeaders;
    }
}
