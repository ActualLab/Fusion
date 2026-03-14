using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ActualLab.Fusion.EntityFramework;
using Samples.TodoApp.Abstractions;
using Samples.TodoApp.Services.Db;

namespace Samples.TodoApp.Services.Auth;

/// <summary>
/// Server-side helper that synchronizes ASP.NET Core authentication state
/// with the local auth services on each HTTP request.
/// </summary>
public class ServerAuthHelper : IHasServices
{
    /// <summary>
    /// Configuration options for <see cref="ServerAuthHelper"/>.
    /// </summary>
    public record Options
    {
        public static Options Default { get; set; } = new();

        public string[] IdClaimKeys { get; init; } = [ClaimTypes.NameIdentifier];
        public string[] NameClaimKeys { get; init; } = [ClaimTypes.Name];
        public string CloseWindowRequestPath { get; init; } = "/fusion/close";
        public TimeSpan SessionInfoUpdatePeriod { get; init; } = TimeSpan.FromSeconds(30);
        public Func<ServerAuthHelper, HttpContext, bool> AllowSignIn = AllowAnywhere;
        public Func<ServerAuthHelper, HttpContext, bool> AllowChange = AllowOnCloseWindowRequest;
        public Func<ServerAuthHelper, HttpContext, bool> AllowSignOut = AllowOnCloseWindowRequest;

        public static bool AllowAnywhere(ServerAuthHelper serverAuthHelper, HttpContext httpContext)
            => true;

        public static bool AllowOnCloseWindowRequest(ServerAuthHelper serverAuthHelper, HttpContext httpContext)
            => serverAuthHelper.IsCloseWindowRequest(httpContext);
    }

    [field: MaybeNull, AllowNull]
    protected IDbShardResolver<AppDbContext> ShardResolver => field ??= Services.GetRequiredService<IDbShardResolver<AppDbContext>>();
    [field: MaybeNull, AllowNull]
    protected ISessionBackend SessionBackend => field ??= Services.GetRequiredService<ISessionBackend>();
    [field: MaybeNull, AllowNull]
    protected IUserBackend UserBackend => field ??= Services.GetRequiredService<IUserBackend>();
    [field: MaybeNull, AllowNull]
    protected IUserApi UserApi => field ??= Services.GetRequiredService<IUserApi>();
    [field: MaybeNull, AllowNull]
    protected ISessionResolver SessionResolver => field ??= Services.GetRequiredService<ISessionResolver>();
    protected ICommander Commander { get; }
    protected MomentClockSet Clocks { get; }

    public Options Settings { get; }
    public IServiceProvider Services { get; }
    public ILogger Log { get; }
    public Session Session => SessionResolver.Session;

#pragma warning disable CA1721
    protected string? Schemas {
#pragma warning restore CA1721
        get;
        set => Interlocked.Exchange(ref field, value);
    }

    public ServerAuthHelper(Options settings, IServiceProvider services)
    {
        Settings = settings;
        Services = services;
        Log = services.LogFor(GetType());

        Commander = services.Commander();
        Clocks = services.Clocks();
    }

    public virtual async ValueTask<string> GetSchemas(HttpContext httpContext, bool cache = true)
    {
        string? schemas;
        if (cache) {
            schemas = Schemas;
            if (schemas is not null)
                return schemas;
        }
        var authSchemas = await httpContext.GetAuthenticationSchemas().ConfigureAwait(false);
        var lSchemas = new List<string>();
        foreach (var authSchema in authSchemas) {
            lSchemas.Add(authSchema.Name);
            lSchemas.Add(authSchema.DisplayName ?? authSchema.Name);
        }
        schemas = ListFormat.Default.Format(lSchemas);
        if (cache)
            Schemas = schemas;
        return schemas;
    }

    public Task UpdateAuthState(HttpContext httpContext, CancellationToken cancellationToken = default)
        => UpdateAuthState(SessionResolver.Session, httpContext, false, cancellationToken);
    public Task UpdateAuthState(HttpContext httpContext, bool assumeAllowed, CancellationToken cancellationToken = default)
        => UpdateAuthState(SessionResolver.Session, httpContext, assumeAllowed, cancellationToken);
    public virtual async Task UpdateAuthState(
        Session session,
        HttpContext httpContext,
        bool assumeAllowed,
        CancellationToken cancellationToken = default)
    {
        var httpUser = httpContext.User;
        var httpAuthenticationSchema = httpUser.Identity?.AuthenticationType ?? "";
        var httpIsSignedIn = !httpAuthenticationSchema.IsNullOrEmpty();

        var ipAddress = httpContext.GetRemoteIPAddress()?.ToString() ?? "";
        var userAgent = httpContext.Request.Headers.TryGetValue("User-Agent", out var userAgentValues)
            ? userAgentValues.FirstOrDefault() ?? ""
            : "";

        var sessionInfo = await SessionBackend.GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
        var mustSetupSession =
            sessionInfo is null
            || !string.Equals(sessionInfo.IPAddress, ipAddress, StringComparison.Ordinal)
            || !string.Equals(sessionInfo.UserAgent, userAgent, StringComparison.Ordinal)
            || sessionInfo.LastSeenAt + Settings.SessionInfoUpdatePeriod < Clocks.SystemClock.Now;
        if (mustSetupSession || sessionInfo is null)
            sessionInfo = await SetupSession(session, ipAddress, userAgent, cancellationToken)
                .ConfigureAwait(false);

        var user = await UserApi.GetOwn(session, cancellationToken).ConfigureAwait(false);
        var isSignedIn = user?.IsAuthenticated() == true;
        try {
            if (httpIsSignedIn) {
                if (isSignedIn && IsSameUser(user, httpUser, httpAuthenticationSchema))
                    return;

                var isSignInAllowed = !isSignedIn
                    ? assumeAllowed || Settings.AllowSignIn(this, httpContext)
                    : assumeAllowed || Settings.AllowChange(this, httpContext);
                if (!isSignInAllowed)
                    return;

                await SignIn(session, user, httpUser, httpAuthenticationSchema, cancellationToken).ConfigureAwait(false);
            }
            else if (isSignedIn && (assumeAllowed || Settings.AllowSignOut(this, httpContext)))
                await SignOut(session, cancellationToken).ConfigureAwait(false);
        }
        finally {
            _ = SessionBackend.UpdatePresence(session, CancellationToken.None);
        }
    }

    public bool IsCloseWindowRequest(HttpContext httpContext)
        => IsCloseWindowRequest(httpContext, out _);
    public virtual bool IsCloseWindowRequest(HttpContext httpContext, out string closeWindowFlowName)
    {
        var request = httpContext.Request;
        var isCloseWindowRequest =
            string.Equals(request.Path.Value, Settings.CloseWindowRequestPath, StringComparison.Ordinal);
        closeWindowFlowName = "";
        if (isCloseWindowRequest && request.Query.TryGetValue("flow", out var flows))
            closeWindowFlowName = flows.FirstOrDefault() ?? "";
        return isCloseWindowRequest;
    }

    // Protected methods

    protected virtual Task<SessionInfo> SetupSession(
        Session session, string ipAddress, string userAgent,
        CancellationToken cancellationToken)
    {
        var setupSessionCommand = new SessionBackend_SetupSession(session, ipAddress, userAgent);
        return Commander.Call(setupSessionCommand, true, cancellationToken);
    }

    protected virtual async Task SignIn(
        Session session, User? user, ClaimsPrincipal httpUser, string httpAuthenticationSchema,
        CancellationToken cancellationToken)
    {
        var (newUser, authenticatedIdentity) = CreateOrUpdateUser(user, httpUser, httpAuthenticationSchema);
        // First upsert the user
        var shard = ShardResolver.Resolve(session);
        var upsertedUser = await Commander.Call(new UserBackend_Upsert(newUser, shard), true, cancellationToken)
            .ConfigureAwait(false);
        // Then sign in the session with the user
        var signInUser = upsertedUser with { Identities = newUser.Identities };
        var signInCommand = new SessionBackend_SignIn(session, signInUser, authenticatedIdentity);
        await Commander.Call(signInCommand, true, cancellationToken).ConfigureAwait(false);
    }

    protected virtual Task SignOut(
        Session session, CancellationToken cancellationToken)
    {
        var signOutCommand = new SessionBackend_SignOut(session);
        return Commander.Call(signOutCommand, true, cancellationToken);
    }

    protected virtual bool IsSameUser(User? user, ClaimsPrincipal httpUser, string schema)
    {
        if (user is null)
            return false;

        var httpUserIdentityName = httpUser.Identity?.Name ?? "";
        var claims = httpUser.Claims
            .GroupBy(c => c.Type, StringComparer.Ordinal)
            .Select(g => (g.Key, Value: g.Select(c => c.Value).ToDelimitedString("\n")))
            .ToImmutableDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var id = FirstClaimOrDefault(claims, Settings.IdClaimKeys) ?? httpUserIdentityName;
        var identity = new UserIdentity(schema, id);
        return user.Identities.ContainsKey(identity);
    }

    protected virtual (User User, UserIdentity AuthenticatedIdentity) CreateOrUpdateUser(
        User? user, ClaimsPrincipal httpUser, string schema)
    {
        var httpUserIdentityName = httpUser.Identity?.Name ?? "";
        var claims = httpUser.Claims
            .GroupBy(c => c.Type, StringComparer.Ordinal)
            .Select(g => (g.Key, Value: g.Select(c => c.Value).ToDelimitedString("\n")))
            .ToApiMap(x => x.Key, x => x.Value, StringComparer.Ordinal);
        var id = FirstClaimOrDefault(claims, Settings.IdClaimKeys) ?? httpUserIdentityName;
        var name = FirstClaimOrDefault(claims, Settings.NameClaimKeys) ?? httpUserIdentityName;
        var identity = new UserIdentity(schema, id);
        var identities = new ApiMap<UserIdentity, string>() {
            { identity, "" },
        };

        if (user is null)
            user = new User("", name) {
                Claims = claims,
                Identities = identities,
            };
        else {
            user = user with {
                Claims = claims.WithMany(user.Claims),
                Identities = identities,
            };
        }
        return (user, identity);
    }

    protected static string? FirstClaimOrDefault(IReadOnlyDictionary<string, string> claims, string[] keys)
    {
        foreach (var key in keys)
            if (claims.TryGetValue(key, out var value) && !value.IsNullOrEmpty())
                return value;
        return null;
    }
}
