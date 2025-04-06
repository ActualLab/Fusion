using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Authentication.Services;

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

        var context = CommandContext.GetCurrent();
        var shard = ShardResolver.Resolve(command);

        if (Invalidation.IsActive) {
            if (isKickCommand)
                return;

            _ = GetSessionInfo(session, default); // Must go first!
            _ = GetAuthInfo(session, default);
            if (force)
                _ = IsSignOutForced(session, default);
            var invSessionInfo = context.Operation.Items.GetKeyless<SessionInfo>();
            if (invSessionInfo != null) {
                _ = GetUser(shard, invSessionInfo.UserId, default);
                _ = GetUserSessions(shard, invSessionInfo.UserId, default);
            }
            return;
        }

        InMemoryOperationScope.Require();
        // Let's handle special kinds of sign-out first, which only trigger "primary" sign-out version
        if (isKickCommand) {
            var user = await GetUser(session, cancellationToken).ConfigureAwait(false);
            if (user == null)
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
        if (sessionInfo == null || sessionInfo.IsSignOutForced)
            return;

        // Updating SessionInfo
        context.Operation.Items.SetKeyless(sessionInfo);
        sessionInfo = sessionInfo with {
            AuthenticatedIdentity = "",
            UserId = "",
            IsSignOutForced = force,
        };
        UpsertSessionInfo(shard, session.Id, sessionInfo, null);
    }

    // [CommandHandler] inherited
    public virtual async Task EditUser(Auth_EditUser command, CancellationToken cancellationToken = default)
    {
        var session = command.Session.RequireValid();
        var context = CommandContext.GetCurrent();
        var shard = ShardResolver.Resolve(command);

        if (Invalidation.IsActive) {
            var invSessionInfo = context.Operation.Items.GetKeyless<SessionInfo>();
            if (invSessionInfo != null)
                _ = GetUser(shard, invSessionInfo.UserId, default);
            return;
        }

        InMemoryOperationScope.Require();
        var sessionInfo = await GetSessionInfo(session, cancellationToken)
            .Require(SessionInfo.MustBeAuthenticated)
            .ConfigureAwait(false);
        var user = await GetUser(shard, sessionInfo.UserId, cancellationToken)
            .Require()
            .ConfigureAwait(false);

        context.Operation.Items.SetKeyless(sessionInfo);
        if (command.Name != null) {
            if (command.Name.Length < 3)
                throw new ArgumentOutOfRangeException(nameof(command));
            user = user with {
                Name = command.Name,
                Version = VersionGenerator.NextVersion(user.Version),
            };
        }
        Users[(shard, user.Id)] = user;
    }

    // [CommandHandler] inherited
    public virtual async Task UpdatePresence(Session session, CancellationToken cancellationToken = default)
    {
        var sessionInfo = await GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
        if (sessionInfo == null)
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
        if (user == null)
            return ImmutableArray<SessionInfo>.Empty;

        var sessions = await GetUserSessions(shard, user.Id, cancellationToken).ConfigureAwait(false);
#if NET8_0_OR_GREATER
        return [..sessions.Select(p => p.SessionInfo)];
#else
        return sessions.Select(p => p.SessionInfo).ToImmutableArray();
#endif
    }
}
