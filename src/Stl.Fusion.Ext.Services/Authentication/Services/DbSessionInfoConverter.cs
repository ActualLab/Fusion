using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Authentication.Services;

public class DbSessionInfoConverter<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbSessionInfo,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbUserId>
    (IServiceProvider services)
    : DbEntityConverter<TDbContext, TDbSessionInfo, SessionInfo>(services)
    where TDbContext : DbContext
    where TDbSessionInfo : DbSessionInfo<TDbUserId>, new()
    where TDbUserId : notnull
{
    protected IDbUserIdHandler<TDbUserId> DbUserIdHandler { get; init; } =
        services.GetRequiredService<IDbUserIdHandler<TDbUserId>>();

    public override TDbSessionInfo NewEntity() => new();
    public override SessionInfo NewModel() => new(Clocks.SystemClock.Now);

    public override void UpdateEntity(SessionInfo source, TDbSessionInfo target)
    {
        var session = new Session(target.Id);
        if (!Equals(session.Hash, source.SessionHash))
            throw new ArgumentOutOfRangeException(nameof(source));
        if (target.IsSignOutForced)
            throw Errors.SessionUnavailable();

        target.Version = VersionGenerator.NextVersion(target.Version);
        target.LastSeenAt = source.LastSeenAt;
        target.IPAddress = source.IPAddress;
        target.UserAgent = source.UserAgent;
        target.Options = source.Options;

        target.AuthenticatedIdentity = source.AuthenticatedIdentity;
        target.UserId = DbUserIdHandler.Parse(source.UserId, true);
        if (DbUserIdHandler.IsNone(target.UserId))
            target.UserId = default; // Should be null instead of None
        target.IsSignOutForced = source.IsSignOutForced;
    }

    public override SessionInfo UpdateModel(TDbSessionInfo source, SessionInfo target)
    {
        var session = new Session(source.Id);
        var result = source.IsSignOutForced
            ? new (session, Clocks.SystemClock.Now) {
                SessionHash = session.Hash,
                IsSignOutForced = true,
            }
            : target with {
                SessionHash = session.Hash,
                Version = source.Version,
                CreatedAt = source.CreatedAt,
                LastSeenAt = source.LastSeenAt,
                IPAddress = source.IPAddress,
                UserAgent = source.UserAgent,
                Options = source.Options,

                // Authentication
                AuthenticatedIdentity = source.AuthenticatedIdentity,
                UserId = DbUserIdHandler.Format(source.UserId),
                IsSignOutForced = source.IsSignOutForced,
            };
        return result;
    }
}
