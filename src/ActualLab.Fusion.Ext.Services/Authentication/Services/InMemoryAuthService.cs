using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Authentication.Services;

/// <summary>
/// In-memory implementation of <see cref="IAuth"/> and <see cref="IAuthBackend"/>
/// for testing and client-side scenarios.
/// </summary>
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public partial class InMemoryAuthService(IServiceProvider services) : IAuth, IAuthBackend
{
    private long _nextUserId;

    protected ConcurrentDictionary<(string Shard, string UserId), User> Users { get; } = new();
    protected ConcurrentDictionary<(string Shard, string SessionId), SessionInfo> SessionInfos { get; } = new();
    protected VersionGenerator<long> VersionGenerator { get; } = services.VersionGenerator<long>();
    protected IDbShardResolver<Unit> ShardResolver { get; } = services.DbShardResolver<Unit>();
    protected MomentClockSet Clocks { get; } = services.Clocks();
    protected ICommander Commander { get; } = services.Commander();

    // Command handlers

    // [CommandHandler] inherited
    public virtual async Task SignOut(Auth_SignOut command, CancellationToken cancellationToken = default)
    {
        var session = command.Session.RequireValid();
        var kickUserSessionHash = command.KickUserSessionHash;
        var kickAllUserSessions = command.KickAllUserSessions;
        var isKickCommand = kickAllUserSessions || !kickUserSessionHash.IsNullOrEmpty();
        var force = command.Force;

        var shard = ShardResolver.Resolve(command);

        TransientOperationScope.Require();
        // Let's handle special kinds of sign-out first, which only trigger "primary" sign-out version
        if (isKickCommand) {
            var user = await GetUser(session, cancellationToken).ConfigureAwait(false);
            if (user is null)
                return;
            var userSessions = await GetUserSessions(shard, user.Id, cancellationToken).ConfigureAwait(false);
            var signOutSessions = kickUserSessionHash.IsNullOrEmpty()
                ? userSessions
                : userSessions.Where(p => Equals(p.SessionInfo.SessionHash, kickUserSessionHash));
            foreach (var (sessionId, _) in signOutSessions) {
                var otherSessionSignOutCommand = new Auth_SignOut(new Session(sessionId), force);
                await Commander.Run(otherSessionSignOutCommand, isOutermost: true, cancellationToken)
                    .ConfigureAwait(false);
            }
            return;
        }

        var sessionInfo = await GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
        if (sessionInfo is null || sessionInfo.IsSignOutForced)
            return;

        // Updating SessionInfo
        var userId = sessionInfo.UserId;
        sessionInfo = sessionInfo with {
            AuthenticatedIdentity = "",
            UserId = "",
            IsSignOutForced = force,
        };
        UpsertSessionInfo(shard, session.Id, sessionInfo, null);

        Invalidation.Defer(() => {
            _ = GetSessionInfo(session, default); // Must go first!
            _ = GetAuthInfo(session, default);
            if (force)
                _ = IsSignOutForced(session, default);
            _ = GetUser(shard, userId, default);
            _ = GetUserSessions(shard, userId, default);
        });
    }

    // [CommandHandler] inherited
    public virtual async Task EditUser(Auth_EditUser command, CancellationToken cancellationToken = default)
    {
        var session = command.Session.RequireValid();
        var shard = ShardResolver.Resolve(command);

        TransientOperationScope.Require();
        var sessionInfo = await GetSessionInfo(session, cancellationToken)
            .Require(SessionInfo.MustBeAuthenticated)
            .ConfigureAwait(false);
        var user = await GetUser(shard, sessionInfo.UserId, cancellationToken)
            .Require()
            .ConfigureAwait(false);

        if (command.Name is not null) {
            if (command.Name.Length < 3)
                throw new ArgumentOutOfRangeException(nameof(command));
            user = user with {
                Name = command.Name,
                Version = VersionGenerator.NextVersion(user.Version),
            };
        }
        Users[(shard, user.Id)] = user;

        Invalidation.Defer(() => _ = GetUser(shard, sessionInfo.UserId, default));
    }

    // [CommandHandler] inherited
    public virtual async Task UpdatePresence(Session session, CancellationToken cancellationToken = default)
    {
        var sessionInfo = await GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
        if (sessionInfo is null)
            return;

        var delta = Clocks.SystemClock.Now - sessionInfo.LastSeenAt;
        if (delta < TimeSpan.FromSeconds(10))
            return; // We don't want to update this too frequently

        var command = new AuthBackend_SetupSession(session);
        await Commander.Call(command, cancellationToken).ConfigureAwait(false);
    }

    // Compute methods

    // [ComputeMethod] inherited
    public virtual Task<SessionInfo?> GetSessionInfo(
        Session session, CancellationToken cancellationToken = default)
    {
        session.RequireValid();
        var shard = ShardResolver.Resolve(session);
        var sessionInfo = SessionInfos.GetValueOrDefault((shard, session.Id));
        return Task.FromResult(sessionInfo)!;
    }

    // [ComputeMethod] inherited
    public virtual async Task<SessionAuthInfo?> GetAuthInfo(
        Session session, CancellationToken cancellationToken = default)
    {
        session.RequireValid();
        using var _ = Computed.BeginIsolation();
        var sessionInfo = await GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
        return sessionInfo?.ToAuthInfo();
    }

    // [ComputeMethod] inherited
    public virtual async Task<bool> IsSignOutForced(Session session, CancellationToken cancellationToken = default)
    {
        using var _ = Computed.BeginIsolation();
        var sessionInfo = await GetAuthInfo(session, cancellationToken).ConfigureAwait(false);
        return sessionInfo?.IsSignOutForced ?? false;
    }

    // [ComputeMethod] inherited
    public virtual async Task<User?> GetUser(Session session, CancellationToken cancellationToken = default)
    {
        session.RequireValid();
        var shard = ShardResolver.Resolve(session);
        var authInfo = await GetAuthInfo(session, cancellationToken).ConfigureAwait(false);
        if (!(authInfo?.IsAuthenticated() ?? false))
            return null;

        var user = await GetUser(shard, authInfo.UserId, cancellationToken).ConfigureAwait(false);
        return user;
    }

    // [ComputeMethod] inherited
    public virtual async Task<ImmutableArray<SessionInfo>> GetUserSessions(
        Session session, CancellationToken cancellationToken = default)
    {
        session.RequireValid();
        var shard = ShardResolver.Resolve(session);
        var user = await GetUser(session, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return ImmutableArray<SessionInfo>.Empty;

        var sessions = await GetUserSessions(shard, user.Id, cancellationToken).ConfigureAwait(false);
#if NET8_0_OR_GREATER
        return [..sessions.Select(p => p.SessionInfo)];
#else
        return sessions.Select(p => p.SessionInfo).ToImmutableArray();
#endif
    }

    // ISessionValidator

    public async Task<bool> IsValidSession(Session session, CancellationToken cancellationToken = default)
        => session.IsValid() && !await IsSignOutForced(session, cancellationToken).ConfigureAwait(false);
}
