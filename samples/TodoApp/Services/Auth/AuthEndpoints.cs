using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Samples.TodoApp.Services.Auth;

/// <summary>
/// Handles sign-in and sign-out HTTP requests using ASP.NET Core authentication.
/// </summary>
public class AuthEndpoints(AuthEndpoints.Options settings)
{
    /// <summary>
    /// Configuration options for <see cref="AuthEndpoints"/>.
    /// </summary>
    public record Options
    {
        public static Options Default { get; set; } = new();

        public string DefaultSignInScheme { get; init; } = "";
        public string DefaultSignOutScheme { get; init; } = CookieAuthenticationDefaults.AuthenticationScheme;
        public Action<HttpContext, AuthenticationProperties>? SignInPropertiesBuilder { get; init; }
        public Action<HttpContext, AuthenticationProperties>? SignOutPropertiesBuilder { get; init; }
    }

    public Options Settings { get; } = settings;

    public virtual Task SignIn(
        HttpContext httpContext,
        string? scheme,
        string? returnUrl)
    {
        scheme = scheme.NullIfEmpty() ?? Settings.DefaultSignInScheme;
        returnUrl ??= "/";
        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        Settings.SignInPropertiesBuilder?.Invoke(httpContext, properties);
        return httpContext.ChallengeAsync(scheme, properties);
    }

    public virtual Task SignOut(
        HttpContext httpContext,
        string? scheme,
        string? returnUrl)
    {
        scheme = scheme.NullIfEmpty() ?? Settings.DefaultSignOutScheme;
        returnUrl ??= "/";
        var properties = new AuthenticationProperties { RedirectUri = returnUrl };
        Settings.SignOutPropertiesBuilder?.Invoke(httpContext, properties);
        return httpContext.SignOutAsync(scheme, properties);
    }
}
