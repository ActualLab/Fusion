using Microsoft.AspNetCore.Http;

namespace ActualLab.Rpc.Server;

/// <summary>
/// Ready-made <see cref="RpcWebSocketServerOriginValidator"/> implementations.
/// All of them allow a request carrying no <c>Origin</c> header: browsers always send it
/// on a WebSocket handshake and page scripts cannot forge it, so only non-browser clients
/// can omit it - and those gain nothing from doing so.
/// </summary>
public static class RpcWebSocketServerOriginValidators
{
    public static readonly RpcWebSocketServerOriginValidator AllowAll =
        static (_, _, _) => true;

    public static readonly RpcWebSocketServerOriginValidator SameOrigin =
        static (_, context, origin) => origin.IsNullOrEmpty() || IsSameOrigin(context.Request, origin);

    public static RpcWebSocketServerOriginValidator Allow(params string[] allowedOrigins)
        => Allow((IEnumerable<string>)allowedOrigins);

    public static RpcWebSocketServerOriginValidator Allow(IEnumerable<string> allowedOrigins)
    {
        // "null" is a valid entry here: sandboxed frames and some mobile WebViews send it verbatim.
        var origins = new HashSet<string>(allowedOrigins.Select(Normalize), StringComparer.Ordinal);
        return (_, _, origin) => origin.IsNullOrEmpty() || origins.Contains(Normalize(origin));
    }

    // Private methods

    private static bool IsSameOrigin(HttpRequest request, string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
            return false; // Also covers the opaque "null" origin
        if (!IsWebScheme(originUri.Scheme))
            return false;

        // The origin's scheme is deliberately not compared with Request.Scheme, and ports are
        // normalized rather than compared verbatim: behind a TLS-terminating proxy Request.Scheme
        // is "http" unless forwarded headers are wired up, while the browser still reports "https".
        // The Host header - which Origin's host must match - survives such a proxy intact.
        var host = request.Host;
        if (!string.Equals(TrimBrackets(originUri.Host), TrimBrackets(host.Host), StringComparison.OrdinalIgnoreCase))
            return false;

        return NormalizePort(originUri.Port) == NormalizePort(host.Port);
    }

    private static bool IsWebScheme(string scheme)
        => string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static int? NormalizePort(int? port)
        => port is null or 80 or 443 ? null : port;

    private static string TrimBrackets(string host)
        => host.Length >= 2 && host[0] == '[' && host[host.Length - 1] == ']'
            ? host.Substring(1, host.Length - 2)
            : host;

    private static string Normalize(string origin)
        => origin.TrimEnd('/').ToLowerInvariant();
}
