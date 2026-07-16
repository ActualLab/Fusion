using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace ActualLab.Fusion.Server.Endpoints;

/// <summary>
/// Determines whether a redirect URL can be used by Fusion server endpoints.
/// </summary>
public delegate bool RedirectUrlChecker(string? url);

internal static class RedirectUrlCheckerExt
{
    private static readonly IUrlHelper UrlHelper = new UrlHelper(new ActionContext(
        new DefaultHttpContext(),
        new RouteData(),
        new ActionDescriptor(),
        new ModelStateDictionary()));

    public static bool IsLocal(string? url)
        => UrlHelper.IsLocalUrl(url);
}
