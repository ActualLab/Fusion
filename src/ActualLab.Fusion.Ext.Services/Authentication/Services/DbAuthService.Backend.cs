using ActualLab.Fusion.Internal;
using ActualLab.Versioning;

namespace ActualLab.Fusion.Authentication.Services;

public partial class DbAuthService<TDbContext, TDbSessionInfo, TDbUser, TDbUserId>
{
    // Commands

    // [CommandHandler] inherited
    public override async Task SignIn(
        AuthBackend_SignIn command, CancellationToken cancellationToken = default)
    {
        var (session, user, authenticatedIdentity) = (command.Session, command.User, command.AuthenticatedIdentity);
        session.RequireValid();

        var shard = ShardResolver.Resolve(command);
        if (!user.Identities.ContainsKey(authenticatedIdentity))
#pragma warning disable MA0015
            throw new ArgumentOutOfRangeException(
                $"{nameof(command)}.{nameof(AuthBackend_SignIn.AuthenticatedIdentity)}");
#pragma warning restore MA0015

        var dbContext = await DbHub.CreateOperationDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var dbSessionInfo = await Sessions.GetOrCreate(dbContext, session.Id, cancellationToken).ConfigureAwait(false);
        var sessionInfo = SessionConverter.ToModel(dbSessionInfo);
        if (sessionInfo!.IsSignOutForced)
            throw Errors.SessionUnavailable();

        var isNewUser = false;
        var dbUser = await Users
            .GetByUserIdentity(dbContext, authenticatedIdentity, true, cancellationToken)
            .ConfigureAwait(false);
        if (dbUser is null) {
            (dbUser, isNewUser) = await Users
                .GetOrCreateOnSignIn(dbContext, user, cancellationToken)
                .ConfigureAwait(false);
            if (isNewUser == false) {
                UserConverter.UpdateEntity(user, dbUser);
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        else {
            user = user with {
                Id = UserIdHandler.Format(dbUser.Id)
            };
            UserConverter.UpdateEntity(user, dbUser);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        sessionInfo = sessionInfo with {
            LastSeenAt = Clocks.SystemClock.Now,
            AuthenticatedIdentity = authenticatedIdentity,
            UserId = UserIdHandler.Format(dbUser.Id)
        };
        await Sessions.Upsert(dbContext, session.Id, sessionInfo, cancellationToken).ConfigureAwait(false);

        var userId = sessionInfo.UserId;
        Invalidation.Defer(() => {
            _ = GetSessionInfo(session, default); // Must go first!
            _ = GetAuthInfo(session, default);
            _ = GetUser(shard, userId, default);
            _ = GetUserSessions(shard, userId, default);
        });
    }

    // [CommandHandler] inherited
    public override async Task<SessionInfo> SetupSession(
        AuthBackend_SetupSession command, CancellationToken cancellationToken = default)
    {
        var (session, ipAddress, userAgent, options) = command;
        session.RequireValid();

        var shard = ShardResolver.Resolve(command);
        var dbContext = await DbHub.CreateOperationDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var dbSessionInfo = await Sessions.Get(dbContext, session.Id, true, cancellationToken).ConfigureAwait(false);
        var isNew = dbSessionInfo is null;
        var now = Clocks.SystemClock.Now;
        var sessionInfo = SessionConverter.ToModel(dbSessionInfo)
            ?? SessionConverter.NewModel() with { SessionHash = session.Hash };
        sessionInfo = sessionInfo with {
            LastSeenAt = now,
            IPAddress = ipAddress.IsNullOrEmpty() ? sessionInfo.IPAddress : ipAddress,
            UserAgent = userAgent.IsNullOrEmpty() ? sessionInfo.UserAgent : userAgent,
            Options = options.SetMany(sessionInfo.Options),
        };
        dbSessionInfo = await Sessions
            .Upsert(dbContext, session.Id, sessionInfo, cancellationToken)
            .ConfigureAwait(false);
        sessionInfo = SessionConverter.ToModel(dbSessionInfo)!;
        Invalidation.Defer(() => {
            _ = GetSessionInfo(session, default); // Must go first!
            if (isNew)
                _ = GetAuthInfo(session, default);
            if (sessionInfo.IsAuthenticated())
                _ = GetUserSessions(shard, sessionInfo.UserId, default);
        });
        return sessionInfo;
    }

    // [CommandHandler] inherited
    public override async Task SetOptions(
        AuthBackend_SetSessionOptions command, CancellationToken cancellationToken = default)
    {
        var (session, options, expectedVersion) = command;
        session.RequireValid();

        var shard = ShardResolver.Resolve(command);
        var dbContext = await DbHub.CreateOperationDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var dbSessionInfo = await Sessions.Get(dbContext, session.Id, true, cancellationToken).ConfigureAwait(false);
        var sessionInfo = SessionConverter.ToModel(dbSessionInfo);
        if (sessionInfo is null)
            throw new KeyNotFoundException();
        VersionChecker.RequireExpected(sessionInfo.Version, expectedVersion);
        sessionInfo = sessionInfo with {
            LastSeenAt = Clocks.SystemClock.Now,
            Options = options,
        };
        await Sessions.Upsert(dbContext, session.Id, sessionInfo, cancellationToken).ConfigureAwait(false);

        Invalidation.Defer(() => _ = GetSessionInfo(session, default));
    }

    // Compute methods

    // [ComputeMethod] inherited
    public override async Task<User?> GetUser(
        string shard, string userId, CancellationToken cancellationToken = default)
    {
        if (!UserIdHandler.TryParse(userId, false, out var dbUserId))
            return null;

        var dbUser = await Users.Get(shard, dbUserId, cancellationToken).ConfigureAwait(false);
        return UserConverter.ToModel(dbUser);
    }

    // Protected methods

    [ComputeMethod]
    protected virtual async Task<ImmutableArray<(string Id, SessionInfo SessionInfo)>> GetUserSessions(
        string shard, string userId, CancellationToken cancellationToken = default)
    {
        if (!UserIdHandler.TryParse(userId, false, out var dbUserId))
            return ImmutableArray<(string Id, SessionInfo SessionInfo)>.Empty;

        var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _2 = tx.ConfigureAwait(false);

        var dbSessions = await Sessions.ListByUser(dbContext, dbUserId, cancellationToken).ConfigureAwait(false);
        var sessions = dbSessions
            .Select(x => (x.Id, SessionConverter.ToModel(x)!))
            .ToImmutableArray();
        return sessions;
    }
}
