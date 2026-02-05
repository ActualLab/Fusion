using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Authentication.Services;

/// <summary>
/// Abstract base class for database-backed authentication services,
/// defining the <see cref="IAuth"/> and <see cref="IAuthBackend"/> contract methods.
/// </summary>
public abstract class DbAuthService<TDbContext>(IServiceProvider services)
    : DbServiceBase<TDbContext>(services), IAuth, IAuthBackend
    where TDbContext : DbContext
{
    /// <summary>
    /// Configuration options for <see cref="DbAuthService{TDbContext}"/>.
    /// </summary>
    public record Options
    {
        // The default should be less than 3 min - see PresenceService.Options
        public TimeSpan MinUpdatePresencePeriod { get; init; } = TimeSpan.FromMinutes(2.75);
    }

    // IAuth
    public abstract Task SignOut(Auth_SignOut command, CancellationToken cancellationToken = default);
    public abstract Task EditUser(Auth_EditUser command, CancellationToken cancellationToken = default);
    public abstract Task UpdatePresence(Session session, CancellationToken cancellationToken = default);
    public abstract Task<bool> IsSignOutForced(Session session, CancellationToken cancellationToken = default);
    public abstract Task<SessionAuthInfo?> GetAuthInfo(Session session, CancellationToken cancellationToken = default);
    public abstract Task<User?> GetUser(Session session, CancellationToken cancellationToken = default);
    public abstract Task<ImmutableArray<SessionInfo>> GetUserSessions(Session session, CancellationToken cancellationToken = default);

    // IAuthBackend
    public abstract Task SignIn(AuthBackend_SignIn command, CancellationToken cancellationToken = default);
    public abstract Task<SessionInfo> SetupSession(AuthBackend_SetupSession command, CancellationToken cancellationToken = default);
    public abstract Task SetOptions(AuthBackend_SetSessionOptions command, CancellationToken cancellationToken = default);
    public abstract Task<SessionInfo?> GetSessionInfo(Session session, CancellationToken cancellationToken = default);
    public abstract Task<User?> GetUser(string shard, string userId, CancellationToken cancellationToken = default);

    // ISessionValidator
    public async Task<bool> IsValidSession(Session session, CancellationToken cancellationToken = default)
        => session.IsValid() && !await IsSignOutForced(session, cancellationToken).ConfigureAwait(false);
}
