#if !NETSTANDARD

using ActualLab.Fusion.Server;
using ActualLab.Fusion.Server.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace ActualLab.Fusion.Authentication.Endpoints;

/// <summary>
/// Handles sign-in and sign-out HTTP requests using ASP.NET Core authentication.
/// </summary>
public class AuthEndpoints(AuthEndpoints.Options settings, RedirectUrlChecker redirectUrlChecker)
{
    /// <summary>
    /// Configuration options for <see cref="AuthEndpoints"/>.
    /// </summary>
    public record Options
    {
        public static Options Default { get; set; } = new();

        public string DefaultSignInScheme { get; init; } = "";
        public string DefaultSignOutScheme { get; init; } = CookieAuthenticationDefaults.AuthenticationScheme;
        public Action<HttpContext, AuthenticationProperties>? SignInPropertiesBuilder { get; init; } = null;
        public Action<HttpContext, AuthenticationProperties>? SignOutPropertiesBuilder { get; init; } = null;
    }

    public Options Settings { get; } = settings;
    protected RedirectUrlChecker RedirectUrlChecker { get; } = redirectUrlChecker;

    public AuthEndpoints(Options settings)
        : this(settings, FusionWebServerBuilder.DefaultRedirectUrlChecker)
    { }

    public virtual Task SignIn(
        HttpContext httpContext,
        string? scheme,
        string? returnUrl)
    {
        scheme = scheme.NullIfEmpty() ?? Settings.DefaultSignInScheme;
        returnUrl ??= "/";
        if (!RedirectUrlChecker.Invoke(returnUrl))
            returnUrl = "/";
        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        Settings.SignInPropertiesBuilder?.Invoke(httpContext, properties);
        return httpContext.ChallengeAsync(scheme, properties);
    }

    public virtual Task SignOut(
        HttpContext httpContext,
        string? scheme,
        string? returnUrl)
    {
        // Instruct the cookies middleware to delete the local cookie created
        // when the user agent is redirected from the external identity provider
        // after a successful authentication flow (e.g Google or Facebook).
        scheme = scheme.NullIfEmpty() ?? Settings.DefaultSignOutScheme;
        returnUrl ??= "/";
        if (!RedirectUrlChecker.Invoke(returnUrl))
            returnUrl = "/";
        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        Settings.SignOutPropertiesBuilder?.Invoke(httpContext, properties);
        return httpContext.SignOutAsync(scheme, properties);
    }
}

#endif
