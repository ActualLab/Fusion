using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace ActualLab.Fusion.Server.Endpoints;

/// <summary>
/// Decides which redirect URLs Fusion server endpoints may use,
/// and reduces them to a relative form.
/// </summary>
public class RedirectUrlHelper
{
    private static readonly IUrlHelper UrlHelper = new UrlHelper(new ActionContext(
        new DefaultHttpContext(),
        new RouteData(),
        new ActionDescriptor(),
        new ModelStateDictionary()));

    public static RedirectUrlHelper Default { get; set; } = new();

    // Empty means "any host" while MustStripHost holds, and "none" once it doesn't
    public string[] AllowedHosts { get; init; } = [];
    // Clearing this lets an absolute URL through as-is, so it requires AllowedHosts to be set
    public bool MustStripHost { get; init; } = true;

    [field: MaybeNull, AllowNull]
    protected ILogger Log => field ??= StaticLog.For(GetType());

    public virtual bool Check(string? url)
    {
        if (url.IsNullOrEmpty())
            return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return IsLocal(url);

        // The path is re-checked rather than trusted: "http://evil.com//attacker.com" reduces to
        // "//attacker.com", which a browser reads as another origin, and "javascript:alert(1)"
        // reduces to "alert(1)", which isn't a path at all.
        return IsAllowedHost(uri.Host) && IsLocal(ToRelativeUrl(uri));
    }

    public virtual string Normalize(string? url, string fallbackUrl)
    {
        if (!Check(url)) {
            // Debug rather than Warning: the URL is caller-supplied on a public endpoint,
            // so anyone can drive this line as fast as they can issue requests
            Log.LogDebug("Redirect URL is rejected, using {FallbackUrl} instead: {Url}", fallbackUrl, url);
            return fallbackUrl;
        }
        if (!MustStripHost || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url!;

        var relativeUrl = ToRelativeUrl(uri);
        Log.LogWarning("Redirect URL must be relative: {Url} -> {RelativeUrl}", url, relativeUrl);
        return relativeUrl;
    }

    // Protected methods

    protected virtual bool IsLocal(string? url)
        => UrlHelper.IsLocalUrl(url);

    protected virtual bool IsAllowedHost(string host)
    {
        if (AllowedHosts.Any(allowedHost => string.Equals(allowedHost, host, StringComparison.OrdinalIgnoreCase)))
            return true;

        // With no allowlist, a host passes only because Normalize is about to drop it -
        // letting one through un-stripped without naming it would be an open redirect
        return AllowedHosts.Length == 0 && MustStripHost;
    }

    protected static string ToRelativeUrl(Uri uri)
        => uri.PathAndQuery + uri.Fragment;
}
