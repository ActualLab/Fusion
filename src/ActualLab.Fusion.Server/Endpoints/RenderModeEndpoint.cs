using ActualLab.Fusion.Blazor;
using Microsoft.AspNetCore.Http;

namespace ActualLab.Fusion.Server.Endpoints;

public class RenderModeEndpoint
{
    public static CookieBuilder Cookie { get; set; } = new() {
        Name = "RenderMode",
        IsEssential = true,
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Expiration = TimeSpan.FromDays(365),
    };

    public static RenderModeDef GetRenderMode(HttpContext context)
    {
        var cookies = context.Request.Cookies;
        var cookieValue = cookies.TryGetValue(Cookie.Name!, out var v) ? v : "";
        return RenderModeDef.GetOrDefault(cookieValue);
    }

    public virtual Task<RedirectResult> Invoke(HttpContext context, string? renderMode, string? redirectTo = null)
    {
        var renderModeValue = RenderModeDef.GetOrDefault(renderMode);
        if (GetRenderMode(context) != renderModeValue) {
            var response = context.Response;
            renderMode = RenderModeDef.All.Single(x => x == renderModeValue).Key;
            response.Cookies.Append(Cookie.Name!, renderMode, Cookie.Build(context));
        }
        if (redirectTo.IsNullOrEmpty())
            redirectTo = "~/";
        return Task.FromResult(new RedirectResult(redirectTo));
    }

    // Nested types

    public class RedirectResult(string url)
#if NET7_0_OR_GREATER
        : Microsoft.AspNetCore.Http.IResult
#endif
    {
        public string Url { get; init; } = url;

#if NET7_0_OR_GREATER
        public virtual Task ExecuteAsync(HttpContext httpContext)
        {
            var actualResult = Results.Redirect(Url);
            return actualResult.ExecuteAsync(httpContext);
        }
#endif
    }
}
