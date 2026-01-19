# Operations Framework: Cheat Sheet

Quick reference for multi-host invalidation.

## Setup

Register operations framework:

```cs
var fusion = services.AddFusion();
fusion.AddOperationReprocessor();

fusion.AddDbContextServices<AppDbContext>(db => {
    db.AddOperations(operations => {
        operations.ConfigureOperationLogReader(_ => new() {
            CheckPeriod = TimeSpan.FromSeconds(1),
        });
    });
});
```

## Command Handler with Operations

```cs
public class OrderService : DbServiceBase<AppDbContext>, IOrderService
{
    public virtual async Task<Order> CreateOrder(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (Invalidation.IsActive) {
            // Runs on ALL cluster nodes after successful execution
            _ = GetOrders(command.UserId, default);
            _ = GetOrderCount(command.UserId, default);
            return default!;
        }

        // Use CreateOperationDbContext for operation logging
        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
        await using var _ = dbContext.ConfigureAwait(false);

        var order = new Order { UserId = command.UserId };
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        return order;
    }
}
```

## DbServiceBase Pattern

```cs
public class MyService : DbServiceBase<AppDbContext>, IMyService
{
    public MyService(IServiceProvider services) : base(services) { }

    [ComputeMethod]
    public virtual async Task<Data> GetData(long id, CancellationToken ct)
    {
        // Read-only: use CreateDbContext
        var db = await DbHub.CreateDbContext(ct);
        await using var _ = db.ConfigureAwait(false);
        return await db.Data.FindAsync([id], ct);
    }

    public virtual async Task UpdateData(UpdateCommand cmd, CancellationToken ct)
    {
        if (Invalidation.IsActive) {
            _ = GetData(cmd.Id, default);
            return;
        }

        // Write: use CreateOperationDbContext
        var db = await DbHub.CreateOperationDbContext(ct);
        await using var _ = db.ConfigureAwait(false);
        // ... make changes ...
        await db.SaveChangesAsync(ct);
    }
}
```

## Invalidation Patterns with Operations Framework

The `Invalidation.IsActive` pattern ensures invalidation runs on ALL cluster nodes:

```cs
public virtual async Task<Unit> UpdateCart(UpdateCartCommand command, CancellationToken cancellationToken)
{
    if (Invalidation.IsActive) {
        // This block runs:
        // 1. After successful execution on the originating node
        // 2. On ALL other cluster nodes via operation log reprocessing
        _ = GetCart(command.CartId, default);
        return default!;
    }

    // Actual command logic (runs only on originating node)
    var cart = await GetCart(command.CartId, cancellationToken);
    // ... apply updates ...
    return default;
}
```

Conditional invalidation:

```cs
if (Invalidation.IsActive) {
    _ = GetOrder(command.OrderId, default);
    if (command.StatusChanged)
        _ = GetOrdersByStatus(command.OldStatus, default);
    return default!;
}
```

Invalidate multiple methods:

```cs
if (Invalidation.IsActive) {
    _ = GetOrder(command.OrderId, default);
    _ = GetOrderList(command.UserId, default);
    _ = GetOrderCount(command.UserId, default);
    return default!;
}
```

## Operation Log Configuration

```cs
operations.ConfigureOperationLogReader(_ => new() {
    CheckPeriod = TimeSpan.FromSeconds(1),    // How often to check for new operations
    MaxCommitDuration = TimeSpan.FromSeconds(5), // Max time to wait for commit
    MaxOperationAge = TimeSpan.FromMinutes(5),   // Ignore operations older than this
});
```

## Database Setup

Ensure your DbContext has operation and event log tables:

```cs
public class AppDbContext : DbContextBase
{
    public DbSet<DbOperation> Operations => Set<DbOperation>();
    public DbSet<DbEvent> Events => Set<DbEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbOperation>().ToTable("_Operations");
        modelBuilder.Entity<DbEvent>().ToTable("_Events");
    }
}
```
