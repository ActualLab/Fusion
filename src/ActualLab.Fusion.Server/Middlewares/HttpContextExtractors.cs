using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace ActualLab.Fusion.Server.Middlewares;

/// <summary>
/// Provides factory methods for creating <see cref="HttpContext"/>-based value extractors
/// used to derive session tags or identifiers from incoming requests.
/// </summary>
public static class HttpContextExtractors
{
    public static Func<HttpContext, int> PortExtractor { get; set; }
        = httpContext => httpContext.Connection.LocalPort;

    public static Func<HttpContext, string> Port(Func<HttpContext, int>? portExtractor = null)
        => httpContext => {
            portExtractor ??= PortExtractor;
            var port = portExtractor.Invoke(httpContext);
            return port.ToString(CultureInfo.InvariantCulture);
        };

    public static Func<HttpContext, string> PortOffset(
        int minPort, int portCount,
        Func<HttpContext, int>? portExtractor = null)
        => httpContext => {
            portExtractor ??= PortExtractor;
            var portOffset = portExtractor.Invoke(httpContext) - minPort;
            if (portOffset < 0 || portOffset >= portCount)
                return "";

            return portOffset.ToString(CultureInfo.InvariantCulture);
        };

    public static Func<HttpContext, string> Header(string headerName)
        => httpContext => {
            var cookies = httpContext.Request.Headers;
            cookies.TryGetValue(headerName, out var value);
            return value.LastOrDefault() ?? "";
        };

    public static Func<HttpContext, string> Cookie(string cookieName)
        => httpContext => {
            var cookies = httpContext.Request.Cookies;
            cookies.TryGetValue(cookieName, out var value);
            return value ?? "";
        };

    public static Func<HttpContext, string> Subdomain(string subdomainSuffix = ".")
        => httpContext => {
            var host = httpContext.Request.Host.Host;
            var suffixIndex = host.IndexOf(subdomainSuffix, StringComparison.Ordinal);
            return suffixIndex <= 0 ? "" : host[..suffixIndex];
        };

    // Combinators

    public static Func<HttpContext, string> Or(
        this Func<HttpContext, string> extractor,
        Func<HttpContext, string> alternativeExtractor)
        => httpContext => {
            var value = extractor.Invoke(httpContext).NullIfEmpty();
            return value ?? alternativeExtractor.Invoke(httpContext);
        };

    public static Func<HttpContext, string> WithPrefix(
        this Func<HttpContext, string> extractor,
        string prefix)
        => httpContext => prefix + extractor.Invoke(httpContext);

    public static Func<HttpContext, string> WithValidator(
        this Func<HttpContext, string> extractor,
        Action<string> validator)
        => httpContext => {
            var value = extractor.Invoke(httpContext);
            validator.Invoke(value);
            return value;
        };

    public static Func<HttpContext, string> WithMapper(
        this Func<HttpContext, string> extractor,
        Func<string, string> mapper)
        => httpContext => {
            var value = extractor.Invoke(httpContext);
            return mapper.Invoke(value);
        };
}
