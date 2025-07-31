using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using ActualLab.Fusion.Authentication;

namespace ActualLab.Fusion.Server.Middlewares;

public class SessionMiddleware : IMiddleware, IHasServices
{
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
        public Func<HttpContext, bool> RequestFilter { get; init; } = _ => true;
        public Func<SessionMiddleware, HttpContext, Task<bool>> ForcedSignOutHandler { get; init; } = DefaultForcedSignOutHandler;
        public Func<Session, HttpContext, Session>? TagProvider { get; init; }

        public static async Task<bool> DefaultForcedSignOutHandler(SessionMiddleware self, HttpContext httpContext)
        {
            await httpContext.SignOutAsync().ConfigureAwait(false);
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

    public IAuth? Auth { get; }
    public ISessionResolver SessionResolver { get; }

    public SessionMiddleware(Options settings, IServiceProvider services)
    {
        Settings = settings;
        Services = services;
        Log = services.LogFor(GetType());

        Auth = services.GetService<IAuth>();
        SessionResolver = services.GetRequiredService<ISessionResolver>();
    }

    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        if (Settings.RequestFilter.Invoke(httpContext))
            SessionResolver.Session = await GetOrCreateSession(httpContext).ConfigureAwait(false);
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
        var originalSession = GetSession(httpContext);
        var session = originalSession;
        if (session is not null && Auth is not null) {
            try {
                var isSignOutForced = await Auth.IsSignOutForced(session, cancellationToken).ConfigureAwait(false);
                if (isSignOutForced) {
                    await Settings.ForcedSignOutHandler(this, httpContext).ConfigureAwait(false);
                    session = null;
                }
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                Log.LogError(e, "Session is unavailable: {Session}", session);
                session = null;
            }
        }
        session ??= Session.New();
        session = Settings.TagProvider?.Invoke(session, httpContext) ?? session;
        if (Settings.AlwaysUpdateCookie || session != originalSession) {
            var cookieName = Settings.Cookie.Name ?? "";
            var responseCookies = httpContext.Response.Cookies;
            responseCookies.Append(cookieName, session.Id, Settings.Cookie.Build(httpContext));
        }
        return session;
    }
}
