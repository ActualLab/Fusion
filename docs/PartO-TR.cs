using Microsoft.EntityFrameworkCore;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.EntityFramework.Operations;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartOTR;

// ============================================================================
// PartO-TR.md snippets: Transient Operations
// ============================================================================

public class AppDbContext(DbContextOptions options) : DbContextBase(options)
{
    public DbSet<DbOperation> Operations => Set<DbOperation>();
    public DbSet<User> Users => Set<User>();
}

public record User
{
    public long UserId { get; init; }
    public string Name { get; set; } = "";
}

public record IncrementCommand(string Key) : ICommand<Unit>;

public record UpdateUserCommand(long UserId, string Name) : ICommand<Unit>;

public record SomeCommand : ICommand<Unit>;

[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class CounterService(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    private readonly ConcurrentDictionary<string, int> _counters = new();

    #region PartOTR_TransientOperation
    // Transient: No database context requested
    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public virtual async Task IncrementCounter(
        IncrementCommand command, CancellationToken cancellationToken = default)
    {
        // No CreateOperationDbContext = transient operation
        _counters.AddOrUpdate(command.Key, 1, (_, v) => v + 1);

        Invalidation.Defer(() => _ = GetCounter(command.Key, default));
    }
    #endregion

    [ComputeMethod] public virtual Task<int> GetCounter(string key, CancellationToken ct) => Task.FromResult(_counters.GetValueOrDefault(key));
}

[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class UserService(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    #region PartOTR_PersistentOperation
    // Persistent: Uses database context
    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public virtual async Task UpdateUser(
        UpdateUserCommand command, CancellationToken cancellationToken = default)
    {
        // CreateOperationDbContext = persistent operation
        await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
        var user = await dbContext.Users.FindAsync(command.UserId);
        user!.Name = command.Name;
        await dbContext.SaveChangesAsync(cancellationToken);

        Invalidation.Defer(() => _ = GetUser(command.UserId, default));
    }
    #endregion

    [ComputeMethod] public virtual Task<User?> GetUser(long id, CancellationToken ct) => Task.FromResult<User?>(null);
}

public class ControllingStorageExample(IServiceProvider services) : DbServiceBase<AppDbContext>(services), IComputeService
{
    #region PartOTR_ControlStorage
    [CommandHandler]
    public virtual async Task SomeCommand(
        SomeCommand command, CancellationToken cancellationToken = default)
    {
        var context = CommandContext.GetCurrent();

        await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
        // ... do work ...

        // Don't store the operation (thus only local invalidation, etc.)
        context.Operation.StoreMode = OperationStoreMode.None;
    }
    #endregion
}
