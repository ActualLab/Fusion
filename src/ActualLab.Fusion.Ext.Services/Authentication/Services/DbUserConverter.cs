using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Authentication.Services;

public class DbUserConverter<TDbContext, TDbUser, TDbUserId>(IServiceProvider services)
    : DbEntityConverter<TDbContext, TDbUser, User>(services)
    where TDbContext : DbContext
    where TDbUser : DbUser<TDbUserId>, new()
    where TDbUserId : notnull
{
    protected IDbUserIdHandler<TDbUserId> DbUserIdHandler { get; init; } =
        services.GetRequiredService<IDbUserIdHandler<TDbUserId>>();

    public override TDbUser NewEntity() => new();
    public override User NewModel() => new("", "");

    public override void UpdateEntity(User source, TDbUser target)
    {
        var targetId = DbUserIdHandler.Format(target.Id);
        if (!string.Equals(targetId, source.Id, StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(source));
        target.Version = VersionGenerator.NextVersion(target.Version);

        // Add + update claims
        target.Claims = target.Claims.SetItems(source.Claims);

        // Add + update identities
        var identities = target.Identities.ToDictionary(ui => ui.Id, StringComparer.Ordinal);
        foreach (var (userIdentity, secret) in source.Identities) {
            if (!userIdentity.IsValid)
                continue;
            var foundIdentity = identities.GetValueOrDefault(userIdentity.Id);
            if (foundIdentity != null) {
                foundIdentity.Secret = secret;
                continue;
            }
            target.Identities.Add(new DbUserIdentity<TDbUserId>() {
                Id = userIdentity.Id,
                DbUserId = target.Id,
                Secret = secret ?? "",
            });
        }
    }

    public override User UpdateModel(TDbUser source, User target)
        => target with {
            Id = DbUserIdHandler.Format(source.Id),
            Version = source.Version,
            Name = source.Name,
            Claims = source.Claims.ToApiMap(),
            Identities = source.Identities.ToApiMap(
                ui => new UserIdentity(ui.Id),
                ui => ui.Secret)
        };
}
