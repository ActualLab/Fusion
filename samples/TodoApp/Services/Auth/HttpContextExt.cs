using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Samples.TodoApp.Services.Auth;

/// <summary>
/// Extension methods for <see cref="HttpContext"/> to retrieve authentication
/// schemes and remote IP addresses.
/// </summary>
public static class HttpContextExt
{
    public static async Task<AuthenticationScheme[]> GetAuthenticationSchemas(this HttpContext httpContext)
    {
        if (httpContext is null)
            throw new ArgumentNullException(nameof(httpContext));

        var schemes = httpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var allSchemes = await schemes.GetAllSchemesAsync().ConfigureAwait(false);
        return (
            from scheme in allSchemes
            where !scheme.DisplayName.IsNullOrEmpty()
            select scheme
            ).ToArray();
    }

    public static IPAddress? GetRemoteIPAddress(this HttpContext context, bool useForwardedForHeaders = true)
    {
        if (useForwardedForHeaders) {
            var headers = context.Request.Headers;
            var forwardedForHeader = headers["CF-Connecting-IP"].FirstOrDefault()
                ?? headers["X-Forwarded-For"].FirstOrDefault();
            if (IPAddress.TryParse(forwardedForHeader, out var ipAddress))
                return ipAddress;
        }
        return context.Connection.RemoteIpAddress;
    }
}
