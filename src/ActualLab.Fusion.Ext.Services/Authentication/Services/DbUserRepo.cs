using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;

namespace ActualLab.Fusion.Authentication.Services;

// ReSharper disable once TypeParameterCanBeVariant
public interface IDbUserRepo<in TDbContext, TDbUser, TDbUserId>
    where TDbContext : DbContext
    where TDbUser : DbUser<TDbUserId>, new()
    where TDbUserId : notnull
{
    Type UserEntityType { get; }

    // Write methods
    Task<TDbUser> Create(TDbContext dbContext, User user, CancellationToken cancellationToken = default);
    Task<(TDbUser DbUser, bool IsCreated)> GetOrCreateOnSignIn(
        TDbContext dbContext, User user, CancellationToken cancellationToken = default);
    Task Edit(
        TDbContext dbContext, TDbUser dbUser, Auth_EditUser command, CancellationToken cancellationToken = default);
    Task Remove(
        TDbContext dbContext, TDbUser dbUser, CancellationToken cancellationToken = default);

    // Read methods
    Task<TDbUser?> Get(DbShard shard, TDbUserId userId, CancellationToken cancellationToken = default);
    Task<TDbUser?> Get(TDbContext dbContext, TDbUserId userId, bool forUpdate, CancellationToken cancellationToken = default);
    Task<TDbUser?> GetByUserIdentity(
        TDbContext dbContext, UserIdentity userIdentity, bool forUpdate, CancellationToken cancellationToken = default);
}

public class DbUserRepo<TDbContext, TDbUser, TDbUserId>(
    DbAuthService<TDbContext>.Options settings,
    IServiceProvider services
    ) : DbServiceBase<TDbContext>(services), IDbUserRepo<TDbContext, TDbUser, TDbUserId>
    where TDbContext : DbContext
    where TDbUser : DbUser<TDbUserId>, new()
    where TDbUserId : notnull
{
    protected DbAuthService<TDbContext>.Options Settings { get; init; } = settings;
    protected IDbUserIdHandler<TDbUserId> DbUserIdHandler { get; init; }
        = services.GetRequiredService<IDbUserIdHandler<TDbUserId>>();
    protected IDbEntityResolver<TDbUserId, TDbUser> UserResolver { get; init; }
        = services.DbEntityResolver<TDbUserId, TDbUser>();
    protected IDbEntityConverter<TDbUser, User> UserConverter { get; init; }
        = services.DbEntityConverter<TDbUser, User>();

    public Type UserEntityType => typeof(TDbUser);

    // Write methods

    public virtual async Task<TDbUser> Create(
        TDbContext dbContext, User user, CancellationToken cancellationToken = default)
    {
        // Creating "base" dbUser
        var id = DbUserIdHandler.Parse(user.Id, true);
        if (DbUserIdHandler.IsNone(id))
            id = DbUserIdHandler.New();
        var dbUser = new TDbUser() {
            Id = id,
            Version = VersionGenerator.NextVersion(),
            Name = user.Name,
            Claims = user.Claims.ToImmutableDictionary(),
        };
        dbContext.Add(dbUser);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        user = user with {
            Id = DbUserIdHandler.Format(dbUser.Id)
        };
        // Updating dbUser from the model to persist user.Identities
        UserConverter.UpdateEntity(user, dbUser);
        dbContext.Update(dbUser);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return dbUser;
    }

    public virtual async Task<(TDbUser DbUser, bool IsCreated)> GetOrCreateOnSignIn(
        TDbContext dbContext, User user, CancellationToken cancellationToken = default)
    {
        var dbUserId = DbUserIdHandler.Parse(user.Id, true);
        TDbUser? dbUser;
        if (!DbUserIdHandler.IsNone(dbUserId)) {
            dbUser = await Get(dbContext, dbUserId, false, cancellationToken).ConfigureAwait(false);
            if (dbUser != null)
                return (dbUser, false);
        }

        // No user found, let's create it
        dbUser = await Create(dbContext, user, cancellationToken).ConfigureAwait(false);
        return (dbUser, true);
    }

    public virtual async Task Edit(TDbContext dbContext, TDbUser dbUser, Auth_EditUser command,
        CancellationToken cancellationToken = default)
    {
        if (command.Name != null) {
            dbUser.Name = command.Name;
            dbUser.Version = VersionGenerator.NextVersion(dbUser.Version);
        }
        dbContext.Update(dbUser);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task Remove(
        TDbContext dbContext, TDbUser dbUser, CancellationToken cancellationToken = default)
    {
        await dbContext.Entry(dbUser).Collection(nameof(DbUser<object>.Identities))
            .LoadAsync(cancellationToken).ConfigureAwait(false);
        if (dbUser.Identities.Count > 0)
            dbContext.RemoveRange(dbUser.Identities);
        dbContext.Remove(dbUser);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // Read methods

    public async Task<TDbUser?> Get(DbShard shard, TDbUserId userId, CancellationToken cancellationToken = default)
        => await UserResolver.Get(shard, userId, cancellationToken).ConfigureAwait(false);

    public virtual async Task<TDbUser?> Get(
        TDbContext dbContext, TDbUserId userId, bool forUpdate, CancellationToken cancellationToken = default)
    {
        var dbUsers = forUpdate
            ? dbContext.Set<TDbUser>().ForNoKeyUpdate()
            : dbContext.Set<TDbUser>();
        var dbUser = await dbUsers
            .FirstOrDefaultAsync(u => Equals(u.Id, userId), cancellationToken)
            .ConfigureAwait(false);
        if (dbUser != null)
            await dbContext.Entry(dbUser).Collection(nameof(DbUser<object>.Identities))
                .LoadAsync(cancellationToken).ConfigureAwait(false);
        return dbUser;
    }

    public virtual async Task<TDbUser?> GetByUserIdentity(
        TDbContext dbContext, UserIdentity userIdentity, bool forUpdate, CancellationToken cancellationToken = default)
    {
        if (!userIdentity.IsValid)
            return null;

        var dbUserIdentities = forUpdate
            ? dbContext.Set<DbUserIdentity<TDbUserId>>().ForNoKeyUpdate()
            : dbContext.Set<DbUserIdentity<TDbUserId>>();
        var id = userIdentity.Id.Value;
        var dbUserIdentity = await dbUserIdentities
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (dbUserIdentity == null)
            return null;

        var user = await Get(dbContext, dbUserIdentity.DbUserId, forUpdate, cancellationToken).ConfigureAwait(false);
        return user;
    }
}
