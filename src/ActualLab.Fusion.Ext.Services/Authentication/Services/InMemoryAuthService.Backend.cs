using System.Globalization;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Operations.Internal;
using ActualLab.Versioning;
using Errors = ActualLab.Fusion.Internal.Errors;

namespace ActualLab.Fusion.Authentication.Services;

public partial class InMemoryAuthService
{
    // Command handlers

    // [CommandHandler] inherited
    public virtual Task SignIn(AuthBackend_SignIn command, CancellationToken cancellationToken = default)
    {
        var (session, user, authenticatedIdentity) = (command.Session, command.User, command.AuthenticatedIdentity);
        session.RequireValid();
        var context = CommandContext.GetCurrent();
        var shard = ShardResolver.Resolve(command);

        if (Invalidation.IsActive) {
            _ = GetSessionInfo(session, default); // Must go first!
            _ = GetAuthInfo(session, default);
            var invSessionInfo = context.Operation.Items.GetKeyless<SessionInfo>();
            if (invSessionInfo != null) {
                _ = GetUser(shard, invSessionInfo.UserId, default);
                _ = GetUserSessions(shard, invSessionInfo.UserId, default);
            }
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        if (!user.Identities.ContainsKey(authenticatedIdentity))
#pragma warning disable MA0015
            throw new ArgumentOutOfRangeException(
                $"{nameof(command)}.{nameof(AuthBackend_SignIn.AuthenticatedIdentity)}");
#pragma warning restore MA0015

        var sessionInfo = SessionInfos.GetValueOrDefault((shard, session.Id));
        sessionInfo ??= new SessionInfo(session, Clocks.SystemClock.Now);
        if (sessionInfo.IsSignOutForced)
            throw Errors.SessionUnavailable();

        var isNewUser = false;

        // First, let's validate user.Id
        if (!user.Id.IsNullOrEmpty())
            _ = long.Parse(user.Id, NumberStyles.Integer, CultureInfo.InvariantCulture);

        // And find the existing user
        var existingUser = GetByUserIdentity(shard, authenticatedIdentity);
        if (existingUser == null && !user.Id.IsNullOrEmpty())
            existingUser = Users.GetValueOrDefault((shard, user.Id));

        if (existingUser != null) {
            // Merge if found
            user = MergeUsers(existingUser, user);
        }
        else {
            // Otherwise, create a new one
            if (user.Id.IsNullOrEmpty())
                user = user with { Id = GetNextUserId() };
            isNewUser = true;
        }

        // Update user.Version
        user = user with {
            Version = VersionGenerator.NextVersion(user.Version),
        };

        // Update SessionInfo
        sessionInfo = sessionInfo with {
            AuthenticatedIdentity = authenticatedIdentity,
            UserId = user.Id,
        };

        // Persist changes
        Users[(shard, user.Id)] = user;
        sessionInfo = UpsertSessionInfo(shard, session.Id, sessionInfo, sessionInfo.Version);
        context.Operation.Items.SetKeyless(sessionInfo);
        context.Operation.Items.SetKeyless(isNewUser);
        return Task.CompletedTask;
    }

    // [CommandHandler] inherited
    public virtual Task<SessionInfo> SetupSession(
        AuthBackend_SetupSession command, CancellationToken cancellationToken = default)
    {
        var (session, ipAddress, userAgent, options) = command;
        session.RequireValid();
        var context = CommandContext.GetCurrent();
        var shard = ShardResolver.Resolve(command);

        if (Invalidation.IsActive) {
            _ = GetSessionInfo(session, default); // Must go first!
            var invIsNew = context.Operation.Items.GetKeyless<bool>();
            if (invIsNew)
                _ = GetAuthInfo(session, default);
            var invSessionInfo = context.Operation.Items.GetKeyless<SessionInfo>();
            if (invSessionInfo?.IsAuthenticated() ?? false)
                _ = GetUserSessions(shard, invSessionInfo.UserId, default);
            return Task.FromResult<SessionInfo>(null!);
        }

        InMemoryOperationScope.Require();
        var sessionInfo = SessionInfos.GetValueOrDefault((shard, session.Id));
        context.Operation.Items.SetKeyless(sessionInfo == null); // invIsNew
        sessionInfo ??= new SessionInfo(session, Clocks.SystemClock.Now);
        sessionInfo = sessionInfo with {
            IPAddress = ipAddress.IsNullOrEmpty() ? sessionInfo.IPAddress : ipAddress,
            UserAgent = userAgent.IsNullOrEmpty() ? sessionInfo.UserAgent : userAgent,
            Options = options.SetMany(sessionInfo.Options),
        };
        sessionInfo = UpsertSessionInfo(shard, session.Id, sessionInfo, sessionInfo.Version);
        context.Operation.Items.SetKeyless(sessionInfo); // invSessionInfo
        return Task.FromResult(sessionInfo);
    }

    // [CommandHandler] inherited
    public virtual Task SetOptions(AuthBackend_SetSessionOptions command, CancellationToken cancellationToken = default)
    {
        var (session, options, baseVersion) = command;
        session.RequireValid();
        var shard = ShardResolver.Resolve(command);

        if (Invalidation.IsActive) {
            _ = GetSessionInfo(session, default);
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        var sessionInfo = SessionInfos.GetValueOrDefault((shard, session.Id));
        if (sessionInfo == null || sessionInfo.IsSignOutForced)
            throw new KeyNotFoundException();

        sessionInfo = sessionInfo with {
            Options = options
        };
        UpsertSessionInfo(shard, session.Id, sessionInfo, baseVersion);
        return Task.CompletedTask;
    }

    // Compute methods

    // [ComputeMethod] inherited
    public virtual Task<User?> GetUser(string shard, string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.GetValueOrDefault((shard, userId)))!;

    // Protected methods

    protected virtual Task<ImmutableArray<(string Id, SessionInfo SessionInfo)>> GetUserSessions(
        string shard, string userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsNullOrEmpty())
            return Task.FromResult(ImmutableArray<(string Id, SessionInfo SessionInfo)>.Empty);

        var result = SessionInfos
            .Where(kv => string.Equals(kv.Key.Shard, shard, StringComparison.Ordinal)
                && string.Equals(kv.Value.UserId, userId, StringComparison.Ordinal))
            .OrderByDescending(kv => kv.Value.LastSeenAt)
            .Select(kv => (kv.Key.SessionId, kv.Value))
            .ToImmutableArray();
        return Task.FromResult(result);
    }

    protected virtual SessionInfo UpsertSessionInfo(string shard, string sessionId, SessionInfo sessionInfo, long? expectedVersion)
    {
        sessionInfo = sessionInfo with {
            Version = VersionGenerator.NextVersion(expectedVersion ?? sessionInfo.Version),
            LastSeenAt = Clocks.SystemClock.Now,
        };
#if NETSTANDARD2_0
        var sessionInfo1 = sessionInfo;
        var expectedVersion1 = expectedVersion;
        SessionInfos.AddOrUpdate((shard, sessionId),
            _ => {
                VersionChecker.RequireExpected(0L, expectedVersion1);
                return sessionInfo1;
            },
            (_, oldSessionInfo) => {
                if (oldSessionInfo.IsSignOutForced)
                    throw Errors.SessionUnavailable();
                VersionChecker.RequireExpected(oldSessionInfo.Version, expectedVersion1);
                return sessionInfo1.CreatedAt == oldSessionInfo.CreatedAt
                    ? sessionInfo1
                    : sessionInfo1 with {
                        CreatedAt = oldSessionInfo.CreatedAt
                    };
            });
#else
        SessionInfos.AddOrUpdate((shard, sessionId),
            static (_, arg) => {
                var (sessionInfo1, expectedVersion1) = arg;
                VersionChecker.RequireExpected(0L, expectedVersion1);
                return sessionInfo1;
            },
            static (_, oldSessionInfo, arg) => {
                var (sessionInfo1, expectedVersion1) = arg;
                if (oldSessionInfo.IsSignOutForced)
                    throw Errors.SessionUnavailable();
                VersionChecker.RequireExpected(oldSessionInfo.Version, expectedVersion1);
                return sessionInfo1.CreatedAt == oldSessionInfo.CreatedAt
                    ? sessionInfo1
                    : sessionInfo1 with {
                        CreatedAt = oldSessionInfo.CreatedAt
                    };
            },
            (sessionInfo, baseVersion: expectedVersion));
#endif
        return SessionInfos.GetValueOrDefault((shard, sessionId)) ?? sessionInfo;
    }

    protected virtual User? GetByUserIdentity(string shard, UserIdentity userIdentity)
        => userIdentity.IsValid
            ? Users.FirstOrDefault(kv => string.Equals(kv.Key.Shard, shard, StringComparison.Ordinal)
                && kv.Value.Identities.ContainsKey(userIdentity)).Value
            : null;

    protected virtual User MergeUsers(User existingUser, User user)
        => existingUser with {
            Claims = existingUser.Claims.WithMany(user.Claims), // Add + replace claims
            Identities = existingUser.Identities.WithMany(user.Identities), // Add + replace identities
        };

    protected string GetNextUserId()
        => Interlocked.Increment(ref _nextUserId).ToString(CultureInfo.InvariantCulture);
}
