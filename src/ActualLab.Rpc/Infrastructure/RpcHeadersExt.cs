namespace ActualLab.Rpc.Infrastructure;

public static class RpcHeadersExt
{
    public static readonly RpcHeader[] Empty = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcHeader[] OrEmpty(this RpcHeader[]? headers)
        => headers ?? Empty;

    public static string? TryGet(this RpcHeader[]? headers, in RpcHeaderKey key)
    {
        if (headers == null || headers.Length == 0)
            return null;

        foreach (var h in headers)
            if (h.Key == key)
                return h.Value;

        return null;
    }

    public static bool TryReplace(this RpcHeader[]? headers, in RpcHeader header)
    {
        if (headers == null || headers.Length == 0)
            return false;

        for (var index = 0; index < headers.Length; index++) {
            var h = headers[index];
            if (h.Key == header.Key) {
                headers[index] = header;
                return true;
            }
        }

        return false;
    }

    public static RpcHeader[] WithOrReplace(this RpcHeader[]? headers, in RpcHeader header)
    {
        if (headers == null || headers.Length == 0)
            return [header];

        if (headers.TryReplace(header))
            return headers;

        var newHeaders = new RpcHeader[headers.Length + 1];
        headers.CopyTo(newHeaders, 0);
        newHeaders[^1] = header;
        return newHeaders;
    }

    public static RpcHeader[] WithUnlessExists(this RpcHeader[]? headers, in RpcHeader header)
    {
        if (headers == null || headers.Length == 0)
            return [header];

        if (headers.TryGet(header.Key) != null)
            return headers;

        var newHeaders = new RpcHeader[headers.Length + 1];
        headers.CopyTo(newHeaders, 0);
        newHeaders[^1] = header;
        return newHeaders;
    }

    public static RpcHeader[] With(this RpcHeader[]? headers, in RpcHeader newHeader)
    {
        if (headers == null || headers.Length == 0)
            return [newHeader];

        var result = new RpcHeader[headers.Length + 1];
        headers.CopyTo(result, 0);
        result[^1] = newHeader;
        return result;
    }

    public static RpcHeader[] With(this RpcHeader[]? headers, in RpcHeader newHeader1, in RpcHeader newHeader2)
    {
        if (headers == null || headers.Length == 0)
            return [newHeader1, newHeader2];

        var result = new RpcHeader[headers.Length + 2];
        headers.CopyTo(result, 0);
        result[^2] = newHeader1;
        result[^1] = newHeader2;
        return result;
    }

    public static RpcHeader[]? WithMany(this RpcHeader[]? headers, IReadOnlyList<RpcHeader> newHeaders)
    {
        var newHeaderCount = newHeaders.Count;
        if (newHeaderCount == 0)
            return headers;

        if (headers == null || (headers.Length is var headersLength && headersLength == 0))
            return newHeaders.ToArray();

        var result = new RpcHeader[headersLength + newHeaderCount];
        headers.CopyTo(result, 0);
        for (var i = 0; i < newHeaderCount; i++)
            result[headersLength + i] = newHeaders[i];
        return result;
    }
}
