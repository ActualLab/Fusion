using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Tests.Model;
using ActualLab.Reflection;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

public interface IUserService : IComputeService
{
    [CommandHandler]
    public Task Create(UserService_Add command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task Update(UserService_Update command, CancellationToken cancellationToken = default);
    [CommandHandler]
    public Task<bool> Delete(UserService_Delete command, CancellationToken cancellationToken = default);

    [ComputeMethod(MinCacheDuration = 60)]
    public Task<User?> Get(long userId, CancellationToken cancellationToken = default);
    [ComputeMethod(MinCacheDuration = 60)]
    public Task<long> Count(CancellationToken cancellationToken = default);

    // Not a CommandHandler!
    public Task UpdateDirectly(UserService_Update command, CancellationToken cancellationToken = default);
    public Task Invalidate();
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record UserService_Add(
    [property: DataMember, MemoryPackOrder(0), Key(0)] User User,
    [property: DataMember, MemoryPackOrder(1), Key(1)] bool OrUpdate = false
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record UserService_Update(
    [property: DataMember, MemoryPackOrder(0), Key(0)] User User
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record UserService_Delete(
    [property: DataMember, MemoryPackOrder(0), Key(0)] User User
) : ICommand<bool>;

public class UserService : DbServiceBase<TestDbContext>, IUserService
{
    private readonly IDbEntityResolver<long, User> _userResolver;

    public bool IsProxy { get; }
    public bool UseEntityResolver { get; set; }

    public UserService(IServiceProvider services) : base(services)
    {
        var type = GetType();
        IsProxy = type != type.NonProxyType();
        _userResolver = services.GetRequiredService<IDbEntityResolver<long, User>>();
    }

    // [CommandHandler]
    public virtual async Task Create(UserService_Add command, CancellationToken cancellationToken = default)
    {
        var (user, orUpdate) = command;
        var existingUser = (User?) null;
        var context = CommandContext.GetCurrent();
        if (Invalidation.IsActive) {
            _ = Get(user.Id, default).AssertCompleted();
            existingUser = context.Operation.Items.KeylessGet<User>();
            if (existingUser == null)
                _ = Count(default).AssertCompleted();
            return;
        }

        var dbContext = await CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);
        dbContext.EnableChangeTracking(false);

        var userId = user.Id;
        if (orUpdate) {
            existingUser = await dbContext.Users.FindAsync(DbKey.Compose(userId), cancellationToken);
            context.Operation.Items.KeylessSet(existingUser);
            if (existingUser != null!)
                dbContext.Users.Update(user);
        }
        if (existingUser == null)
            dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // [CommandHandler]
    public virtual async Task Update(UserService_Update command, CancellationToken cancellationToken = default)
    {
        var user = command.User;
        if (Invalidation.IsActive) {
            _ = Get(user.Id, default).AssertCompleted();
            return;
        }

        var dbContext = await CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // Not a CommandHandler!
    public async Task UpdateDirectly(UserService_Update command, CancellationToken cancellationToken = default)
    {
        var user = command.User;
        var dbContext = await DbHub.CreateDbContext(true, cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (Invalidation.IsActive)
            _ = Get(user.Id, default).AssertCompleted();
    }

    public virtual async Task<bool> Delete(UserService_Delete command, CancellationToken cancellationToken = default)
    {
        var user = command.User;
        var context = CommandContext.GetCurrent();
        if (Invalidation.IsActive) {
            var success = context.Operation.Items.KeylessGet<bool>();
            if (success) {
                _ = Get(user.Id, default).AssertCompleted();
                _ = Count(default).AssertCompleted();
            }
            return false;
        }

        var dbContext = await CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        dbContext.Users.Remove(user);
        try {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            context.Operation.Items.KeylessSet(true);
            return true;
        }
        catch (DbUpdateConcurrencyException) {
            return false;
        }
    }

    public virtual async Task<User?> Get(long userId, CancellationToken cancellationToken = default)
    {
        // Debug.WriteLine($"Get {userId}");
        await Everything().ConfigureAwait(false);

        if (UseEntityResolver)
            return await _userResolver.Get(userId, cancellationToken).ConfigureAwait(false);

        var dbContext = await DbHub.CreateDbContext(cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);

        var user = await dbContext.Users
            .FindAsync([(object)userId], cancellationToken)
            .ConfigureAwait(false);
        return user;
    }

    public virtual async Task<long> Count(CancellationToken cancellationToken = default)
    {
        await Everything().ConfigureAwait(false);

        var dbContext = await DbHub.CreateDbContext(cancellationToken).ConfigureAwait(false);
        await using var _ = dbContext.ConfigureAwait(false);

        var count = await dbContext.Users.AsQueryable()
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
        // _log.LogDebug($"Users.Count query: {count}");
        return count;
    }

    public virtual Task Invalidate()
    {
        if (!IsProxy)
            return Task.CompletedTask;

        using (Invalidation.Begin())
            _ = Everything().AssertCompleted();

        return Task.CompletedTask;
    }

    // Protected & private methods

    [ComputeMethod]
    protected virtual Task<Unit> Everything() => TaskExt.UnitTask;

    private ValueTask<TestDbContext> CreateOperationDbContext(CancellationToken cancellationToken = default)
        => IsProxy
            ? DbHub.CreateOperationDbContext(cancellationToken)
            : DbHub.CreateDbContext(readWrite: true, cancellationToken);
}
