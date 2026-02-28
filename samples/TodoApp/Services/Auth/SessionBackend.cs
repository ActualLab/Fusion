using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Internal;
using Samples.TodoApp.Services.Db;

namespace Samples.TodoApp.Services.Auth;

/// <summary>
/// Database-backed implementation of <see cref="ISessionBackend"/>.
/// </summary>
public class SessionBackend(IServiceProvider services) : DbServiceBase<AppDbContext>(services), ISessionBackend
{
    private static readonly TimeSpan MinUpdatePresencePeriod = TimeSpan.FromMinutes(2.75);

    private IDbShardResolver<AppDbContext> ShardResolver { get; } = services.GetRequiredService<IDbShardResolver<AppDbContext>>();
    private IDbEntityResolver<string, DbSessionInfo> SessionResolver { get; } = services.DbEntityResolver<string, DbSessionInfo>();

    // Compute methods

    public virtual async Task<SessionInfo?> GetSessionInfo(
        Session session, CancellationToken cancellationToken = default)
    {
        session.RequireValid();
        var shard = ShardResolver.Resolve(session);
        var dbSessionInfo = await SessionResolver.Get(shard, session.Id, cancellationToken).ConfigureAwait(false);
        return dbSessionInfo?.ToModel();
    }

    [ComputeMethod]
    public virtual async Task<ImmutableArray<SessionInfo>> GetUserSessions(UserId userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsNone)
            return ImmutableArray<SessionInfo>.Empty;

        var shard = ShardResolver.Resolve(userId);
        var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var _2 = tx.ConfigureAwait(false);

        var userIdValue = userId.Id.Value;
        var dbSessions = await dbContext.Set<DbSessionInfo>()
            .Where(s => s.UserId == userIdValue)
            .OrderByDescending(s => s.LastSeenAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return [..dbSessions.Select(s => s.ToModel())];
    }

    // Command handlers

    public virtual async Task<SessionInfo> OnSetupSession(
        SessionBackend_SetupSession command, CancellationToken cancellationToken = default)
    {
        var (session, ipAddress, userAgent, options) = command;
        session.RequireValid();
        var shard = ShardResolver.Resolve(command);

        var context = CommandContext.GetCurrent();
        if (Invalidation.IsActive) {
            var invSessionInfo = context.Operation.Items.KeylessGet<SessionInfo>();
            if (invSessionInfo is null)
                return null!;

            _ = GetSessionInfo(session, default);
            var invIsNew = context.Operation.Items.KeylessGet<bool>();
            if (invIsNew)
                ; // Could invalidate auth info if needed
            if (invSessionInfo.IsAuthenticated())
                _ = GetUserSessions(invSessionInfo.UserId, default);
            return null!;
        }

        var dbContext = await DbHub.CreateOperationDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var dbSessionInfo = await dbContext.Set<DbSessionInfo>().ForNoKeyUpdate()
            .FirstOrDefaultAsync(s => s.Id == session.Id, cancellationToken)
            .ConfigureAwait(false);
        var isNew = dbSessionInfo is null;
        var now = Clocks.SystemClock.Now;
        var sessionInfo = dbSessionInfo is null
            ? new SessionInfo(session, now)
            : dbSessionInfo.ToModel();
        sessionInfo = sessionInfo with {
            LastSeenAt = now,
            IPAddress = ipAddress.IsNullOrEmpty() ? sessionInfo.IPAddress : ipAddress,
            UserAgent = userAgent.IsNullOrEmpty() ? sessionInfo.UserAgent : userAgent,
            Options = options.SetMany(sessionInfo.Options),
        };
        dbSessionInfo = await UpsertSession(dbContext, session.Id, sessionInfo, dbSessionInfo, cancellationToken)
            .ConfigureAwait(false);
        sessionInfo = dbSessionInfo.ToModel();
        context.Operation.Items.KeylessSet(sessionInfo);
        context.Operation.Items.KeylessSet(isNew);
        return sessionInfo;
    }

    public virtual async Task OnSignIn(
        SessionBackend_SignIn command, CancellationToken cancellationToken = default)
    {
        var (session, user, authenticatedIdentity) = (command.Session, command.User, command.AuthenticatedIdentity);
        session.RequireValid();
        var shard = ShardResolver.Resolve(command);

        var context = CommandContext.GetCurrent();
        if (Invalidation.IsActive) {
            _ = GetSessionInfo(session, default);
            var invSessionInfo = context.Operation.Items.KeylessGet<SessionInfo>();
            if (invSessionInfo is not null)
                _ = GetUserSessions(invSessionInfo.UserId, default);
            return;
        }

        if (!user.Identities.ContainsKey(authenticatedIdentity))
#pragma warning disable MA0015
            throw new ArgumentOutOfRangeException(
                $"{nameof(command)}.{nameof(SessionBackend_SignIn.AuthenticatedIdentity)}");
#pragma warning restore MA0015

        var dbContext = await DbHub.CreateOperationDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var dbSessionInfo = await GetOrCreateSession(dbContext, session, cancellationToken).ConfigureAwait(false);
        if (dbSessionInfo.IsSignOutForced)
            throw Errors.SessionUnavailable();

        var sessionInfo = dbSessionInfo.ToModel() with {
            LastSeenAt = Clocks.SystemClock.Now,
            AuthenticatedIdentity = authenticatedIdentity,
            UserId = user.Id,
        };
        await UpsertSession(dbContext, session.Id, sessionInfo, dbSessionInfo, cancellationToken).ConfigureAwait(false);
        context.Operation.Items.KeylessSet(sessionInfo);
    }

    public virtual async Task OnSignOut(
        SessionBackend_SignOut command, CancellationToken cancellationToken = default)
    {
        var session = command.Session.RequireValid();
        var kickUserSessionHash = command.KickUserSessionHash;
        var kickAllUserSessions = command.KickAllUserSessions;
        var isKickCommand = kickAllUserSessions || !kickUserSessionHash.IsNullOrEmpty();
        var force = command.Force;
        var shard = ShardResolver.Resolve(command);

        var context = CommandContext.GetCurrent();
        if (Invalidation.IsActive) {
            if (isKickCommand)
                return;

            _ = GetSessionInfo(session, default);
            var invSessionInfo = context.Operation.Items.KeylessGet<SessionInfo>();
            if (invSessionInfo is not null)
                _ = GetUserSessions(invSessionInfo.UserId, default);
            return;
        }

        // Handle kick commands
        if (isKickCommand) {
            var sessionInfo = await GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
            if (sessionInfo is null || !sessionInfo.IsAuthenticated())
                return;

            UserId userId = sessionInfo.UserId;
            var userSessionIds = await GetUserSessionIds(userId, cancellationToken)
                .ConfigureAwait(false);
            var signOutSessionIds = kickUserSessionHash.IsNullOrEmpty()
                ? userSessionIds
                : userSessionIds
                    .Where(p => Equals(new Session(p).Hash, kickUserSessionHash));
            foreach (var sessionId in signOutSessionIds) {
                var otherSignOutCommand = new SessionBackend_SignOut(new Session(sessionId), force);
                await Commander.Run(otherSignOutCommand, isOutermost: true, cancellationToken)
                    .ConfigureAwait(false);
            }
            return;
        }

        var dbContext = await DbHub.CreateOperationDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var dbSessionInfo = await GetOrCreateSession(dbContext, session, cancellationToken).ConfigureAwait(false);
        var si = dbSessionInfo.ToModel();
        if (si.IsSignOutForced)
            return;

        context.Operation.Items.KeylessSet(si);
        si = si with {
            LastSeenAt = Clocks.SystemClock.Now,
            AuthenticatedIdentity = "",
            UserId = "",
            IsSignOutForced = force,
        };
        await UpsertSession(dbContext, session.Id, si, dbSessionInfo, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task UpdatePresence(
        Session session, CancellationToken cancellationToken = default)
    {
        var sessionInfo = await GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
        if (sessionInfo is null)
            return;

        var delta = Clocks.SystemClock.Now - sessionInfo.LastSeenAt;
        if (delta < MinUpdatePresencePeriod)
            return;

        var command = new SessionBackend_SetupSession(session);
        await Commander.Call(command, cancellationToken).ConfigureAwait(false);
    }

    // ISessionValidator

    public async Task<bool> IsValidSession(Session session, CancellationToken cancellationToken = default)
    {
        var sessionInfo = await GetSessionInfo(session, cancellationToken).ConfigureAwait(false);
        return session.IsValid() && sessionInfo is not { IsSignOutForced: true };
    }

    // Private methods

    private async Task<string[]> GetUserSessionIds(
        UserId userId, CancellationToken cancellationToken)
    {
        var shard = ShardResolver.Resolve(userId);
        var dbContext = await DbHub.CreateDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        var userIdValue = userId.Id.Value;
        return await dbContext.Set<DbSessionInfo>()
            .Where(s => s.UserId == userIdValue)
            .Select(s => s.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DbSessionInfo> GetOrCreateSession(
        AppDbContext dbContext, Session session, CancellationToken cancellationToken)
    {
        var dbSessionInfo = await dbContext.Set<DbSessionInfo>().ForNoKeyUpdate()
            .FirstOrDefaultAsync(s => s.Id == session.Id, cancellationToken)
            .ConfigureAwait(false);
        if (dbSessionInfo is null) {
            var now = Clocks.SystemClock.Now;
            dbSessionInfo = new DbSessionInfo {
                Id = session.Id,
                CreatedAt = now,
                LastSeenAt = now,
            };
            dbSessionInfo.UpdateFrom(new SessionInfo(session, now), VersionGenerator);
            dbContext.Add(dbSessionInfo);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return dbSessionInfo;
    }

    private async Task<DbSessionInfo> UpsertSession(
        AppDbContext dbContext, string sessionId, SessionInfo sessionInfo,
        DbSessionInfo? existing, CancellationToken cancellationToken)
    {
        var dbSessionInfo = existing ?? await dbContext.Set<DbSessionInfo>().ForNoKeyUpdate()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        var isFound = dbSessionInfo is not null;
        dbSessionInfo ??= new DbSessionInfo {
            Id = sessionId,
            CreatedAt = sessionInfo.CreatedAt,
        };
        dbSessionInfo.UpdateFrom(sessionInfo, VersionGenerator);
        if (isFound)
            dbContext.Update(dbSessionInfo);
        else
            dbContext.Add(dbSessionInfo);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return dbSessionInfo;
    }

}
