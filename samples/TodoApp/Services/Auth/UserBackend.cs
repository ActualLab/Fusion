using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using Samples.TodoApp.Abstractions;
using Samples.TodoApp.Services.Db;

namespace Samples.TodoApp.Services.Auth;

/// <summary>
/// Database-backed implementation of <see cref="IUserBackend"/>.
/// </summary>
public class UserBackend(IServiceProvider services)
    : DbServiceBase<AppDbContext>(services), IUserBackend
{
    private IDbShardResolver<AppDbContext> ShardResolver { get; } = services.GetRequiredService<IDbShardResolver<AppDbContext>>();
    private IDbEntityResolver<string, DbUser> UserResolver { get; } = services.DbEntityResolver<string, DbUser>();

    // Compute methods

    public virtual async Task<User?> Get(UserId userId, CancellationToken cancellationToken = default)
    {
        if (userId.IsNone)
            return null;

        var shard = ShardResolver.Resolve(userId);
        var dbUser = await UserResolver.Get(shard, userId.Id.Value, cancellationToken).ConfigureAwait(false);
        return dbUser?.ToModel();
    }

    // Command handlers

    public virtual async Task<User> OnUpsert(UserBackend_Upsert command, CancellationToken cancellationToken = default)
    {
        var user = command.User;
        var shard = ShardResolver.Resolve(command);

        var context = CommandContext.GetCurrent();
        if (Invalidation.IsActive) {
            var invUser = context.Operation.Items.KeylessGet<User>();
            if (invUser is not null)
                _ = Get(invUser.Id, default);
            return null!;
        }

        var dbContext = await DbHub.CreateOperationDbContext(shard, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        DbUser? dbUser = null;

        // Try to find by identity first
        if (!user.Identities.IsEmpty) {
            var identity = user.Identities.First().Key;
            if (identity.IsValid) {
                var id = identity.Id;
                var dbUserIdentity = await dbContext.Set<DbUserIdentity>().ForNoKeyUpdate()
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                    .ConfigureAwait(false);
                if (dbUserIdentity is not null) {
                    dbUser = await dbContext.Set<DbUser>().ForNoKeyUpdate()
                        .Include(u => u.Identities)
                        .FirstOrDefaultAsync(u => u.Id == dbUserIdentity.DbUserId, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        // Try to find by ID
        if (dbUser is null && !user.Id.IsNullOrEmpty()) {
            dbUser = await dbContext.Set<DbUser>().ForNoKeyUpdate()
                .Include(u => u.Identities)
                .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        if (dbUser is null) {
            // Create new user with shard-prefixed ID
            var id = user.Id.IsNullOrEmpty()
                ? UserId.New(shard, ActualLab.Generators.RandomStringGenerator.Default.Next()).Id.Value
                : user.Id;
            dbUser = new DbUser {
                Id = id,
                Version = VersionGenerator.NextVersion(),
                Name = user.Name,
                Claims = user.Claims.ToImmutableDictionary(StringComparer.Ordinal),
            };
            dbContext.Add(dbUser);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Now add identities
            user = user with { Id = id };
            dbUser.UpdateFrom(user, VersionGenerator);
            dbContext.Update(dbUser);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else {
            // Update existing user
            dbUser.UpdateFrom(user with { Id = dbUser.Id }, VersionGenerator);
            dbContext.Update(dbUser);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = dbUser.ToModel();
        context.Operation.Items.KeylessSet(result);
        return result;
    }

}
