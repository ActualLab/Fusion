using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace ActualLab.Fusion.Server.Middlewares;

/// <summary>
/// ASP.NET Core middleware that resolves or creates a <see cref="Session"/>
/// from cookies and makes it available via <see cref="ISessionResolver"/>.
/// </summary>
public class SessionMiddleware : IMiddleware, IHasServices
{
    /// <summary>
    /// Configuration options for <see cref="SessionMiddleware"/>.
    /// </summary>
    public record Options
    {
        public static Options Default { get; set; } = new();

        public CookieBuilder Cookie { get; init; } = new() {
            Name = "FusionAuth.SessionId",
            IsEssential = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expiration = TimeSpan.FromDays(28),
        };
        public bool AlwaysUpdateCookie { get; init; } = true; // This ensures cookie expiration time gets bumped up on each request
        public string ReloadGuardCookieName { get; init; } = "FusionAuth.SessionReload"; // "" disables the anti-redirect-loop guard
        public Func<HttpContext, bool> RequestFilter { get; init; } = _ => true;
        public Func<SessionMiddleware, HttpContext, Task<bool>> InvalidSessionHandler { get; init; } = DefaultInvalidSessionHandler;
        public Func<Session, HttpContext, Session>? TagProvider { get; init; }

        public static async Task<bool> DefaultInvalidSessionHandler(SessionMiddleware self, HttpContext httpContext)
        {
            // A parameterless SignOutAsync throws when there is no default sign-out scheme,
            // which would turn an invalid session into a permanent 500 in apps not using ASP.NET auth.
            var schemes = httpContext.RequestServices.GetService<IAuthenticationSchemeProvider>();
            var scheme = schemes is null
                ? null
                : await schemes.GetDefaultSignOutSchemeAsync().ConfigureAwait(false);
            if (scheme is not null) {
                try {
                    await httpContext.SignOutAsync(scheme.Name).ConfigureAwait(false);
                }
                catch (Exception e) {
                    self.Log.LogWarning(e, "Sign-out failed for '{Scheme}' scheme", scheme.Name);
                }
            }
            var url = httpContext.Request.GetEncodedPathAndQuery();
            httpContext.Response.Redirect(url);
            // true: reload: redirect w/o invoking the next middleware
            // false: proceed normally, i.e., invoke the next middleware
            return true;
        }
    }

    public Options Settings { get; }
    public IServiceProvider Services { get; }
    public ILogger Log { get; }

    public ISessionResolver SessionResolver { get; }
    public ISessionValidator? SessionValidator { get; }

    public SessionMiddleware(Options settings, IServiceProvider services)
    {
        Settings = settings;
        Services = services;
        Log = services.LogFor(GetType());

        SessionResolver = services.GetRequiredService<ISessionResolver>();
        SessionValidator = services.GetService<ISessionValidator>();
    }

    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        if (Settings.RequestFilter.Invoke(httpContext)) {
            var session = await GetOrCreateSession(httpContext).ConfigureAwait(false);
            if (httpContext.Features.Get<MustShortCircuitFeature>() is not null)
                return;

            SessionResolver.Session = session;
        }
        await next(httpContext).ConfigureAwait(false);
    }

    public virtual Session? GetSession(HttpContext httpContext)
    {
        var cookies = httpContext.Request.Cookies;
        var cookieName = Settings.Cookie.Name ?? "";
        cookies.TryGetValue(cookieName, out var sessionId);
        return sessionId.IsNullOrEmpty() ? null : new Session(sessionId);
    }

    public virtual async Task<Session> GetOrCreateSession(HttpContext httpContext)
    {
        var cancellationToken = httpContext.RequestAborted;
        var originalSession = (Session?)null;
        var session = (Session?)null;
        var isInvalid = false;
        try {
            originalSession = GetSession(httpContext);
            session = originalSession;
            if (session is not null && SessionValidator is not null) {
                var isValid = await SessionValidator.IsValidSession(session, cancellationToken).ConfigureAwait(false);
                isInvalid = !isValid;
            }
        }
        catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
            Log.LogError(e, "Session is unavailable: {SessionHash}", session?.Hash);
            isInvalid = true;
        }
        if (isInvalid)
            session = null;
        session ??= Session.New();
        session = Settings.TagProvider?.Invoke(session, httpContext) ?? session;
        // The cookie must be rewritten before InvalidSessionHandler gets a chance to short-circuit
        // the pipeline - otherwise every redirected request re-presents the rejected session id.
        if (Settings.AlwaysUpdateCookie || session != originalSession) {
            var cookieName = Settings.Cookie.Name ?? "";
            var responseCookies = httpContext.Response.Cookies;
            responseCookies.Append(cookieName, session.Id, Settings.Cookie.Build(httpContext));
        }
        if (!isInvalid) {
            RemoveReloadGuard(httpContext);
            return session;
        }

        if (HasReloadGuard(httpContext)) {
            // The reload didn't help, so we proceed with a fresh session instead of redirecting again
            Log.LogWarning("Session is still invalid right after a reload: {SessionHash}", originalSession?.Hash);
            RemoveReloadGuard(httpContext);
            return session;
        }

        if (await Settings.InvalidSessionHandler(this, httpContext).ConfigureAwait(false)) {
            AddReloadGuard(httpContext);
            httpContext.Features.Set(MustShortCircuitFeature.Instance);
        }
        return session;
    }

    // Private methods

    private bool HasReloadGuard(HttpContext httpContext)
    {
        var cookieName = Settings.ReloadGuardCookieName;
        return !cookieName.IsNullOrEmpty() && httpContext.Request.Cookies.ContainsKey(cookieName);
    }

    private void AddReloadGuard(HttpContext httpContext)
    {
        var cookieName = Settings.ReloadGuardCookieName;
        if (cookieName.IsNullOrEmpty())
            return;

        httpContext.Response.Cookies.Append(cookieName, "1", GetReloadGuardCookieOptions(httpContext));
    }

    private void RemoveReloadGuard(HttpContext httpContext)
    {
        if (!HasReloadGuard(httpContext))
            return;

        httpContext.Response.Cookies.Delete(Settings.ReloadGuardCookieName, GetReloadGuardCookieOptions(httpContext));
    }

    private CookieOptions GetReloadGuardCookieOptions(HttpContext httpContext)
    {
        // The guard must expire with the browser session rather than live as long as the session cookie
        var options = Settings.Cookie.Build(httpContext);
        options.Expires = null;
        options.MaxAge = null;
        return options;
    }

    // Nested types

    private sealed class MustShortCircuitFeature
    {
        public static MustShortCircuitFeature Instance { get; } = new();
    }
}
