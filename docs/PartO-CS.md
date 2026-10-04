# Operations Framework: Cheat Sheet

Quick reference for multi-host invalidation, events, and operation reprocessing.

## Setup

### Basic Configuration

<!-- snippet: PartOCS_BasicConfiguration -->
```cs
var fusion = services.AddFusion();
fusion.AddOperationReprocessor();  // Enable retry for transient errors

services.AddDbContextServices<AppDbContext>(db => {
    db.AddOperations(operations => {
        operations.ConfigureOperationLogReader(_ => new() {
            CheckPeriod = TimeSpan.FromSeconds(5).ToRandom(0.1),
        });

        // Choose one watcher:
        operations.AddNpgsqlOperationLogWatcher();    // PostgreSQL
        // operations.AddRedisOperationLogWatcher();  // Redis
        // operations.AddFileSystemOperationLogWatcher();  // Local dev
    });
});
```
<!-- endSnippet -->

### DbContext Setup

<!-- snippet: PartOCS_DbContextSetup -->
```cs
public DbSet<DbOperation> Operations => Set<DbOperation>();
public DbSet<DbEvent> Events => Set<DbEvent>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<DbOperation>().ToTable("_Operations");
    modelBuilder.Entity<DbEvent>().ToTable("_Events");
}
```
<!-- endSnippet -->

## Command Handler Pattern

<!-- snippet: PartOCS_CommandHandlerPattern -->
```cs
[CommandHandler]
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public virtual async Task<Order> CreateOrder(
    CreateOrderCommand command, CancellationToken cancellationToken = default)
{
    // 1. MUTATE
    await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);

    var order = new Order { /* ... */ };
    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync(cancellationToken);

    // 2. DECLARE THE INVALIDATION - the block runs after the commit
    Invalidation.Defer(() => {
        _ = GetOrder(command.OrderId, default);
        _ = GetOrdersByUser(command.UserId, default);
    });
    return order;
}
```
<!-- endSnippet -->

## Invalidation Modes

See [Invalidation Modes](./PartO-IM.md) for the full picture.

| Mode | Declared with | Reach |
|------|---------------|-------|
| `Local` | `Invalidation.Defer(...)` | the origin host |
| `Replicated` | `Invalidation.Defer(...)` | every host |
| `Distributed` | `Invalidation.Defer(...)` | the owner of each value |

<!-- snippet: PartOCS_DeferredInvalidation -->
```cs
// What to invalidate depends on what the mutation found, and a deferred block
// is an ordinary closure - so the condition is just evaluated here
[CommandHandler]
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public virtual async Task DeleteUserDeferred(
    DeleteUserCommand command, CancellationToken cancellationToken = default)
{
    await using var db = await DbHub.CreateOperationDbContext(cancellationToken);
    var user = await db.Users.FindAsync(command.UserId);

    db.Users.Remove(user!);
    await db.SaveChangesAsync(cancellationToken);

    Invalidation.Defer(() => _ = GetUser(user!.Id, default));
}
```
<!-- endSnippet -->

There is no application-wide default: declare the mode on the handler method, on its implementation
type, or on the service interface. To resolve it some other way &ndash; per namespace, per tenant,
from configuration &ndash; register your own `DeferredInvalidationModeResolver`.

`Replicated` and `Distributed` both require an operation scope that stores its operation;
`Distributed` additionally requires `KeepProcessedItems` to stay `true` on the event log reader.

## Events

### Adding Events

<!-- snippet: PartOCS_AddingEvents -->
```cs
[CommandHandler]
public virtual async Task<Order> CreateOrderWithEvent(
    CreateOrderCommand command, CancellationToken cancellationToken = default)
{
    var context = CommandContext.GetCurrent();
    await using var db = await DbHub.CreateOperationDbContext(cancellationToken);

    var order = new Order { /* ... */ };
    db.Orders.Add(order);
    await db.SaveChangesAsync(cancellationToken);

    // Add event (processed asynchronously after commit)
    context.Operation.AddEvent(new SendOrderConfirmationCommand(order.Id));

    return order;
}
```
<!-- endSnippet -->

### Delayed Events

<!-- snippet: PartOCS_DelayedEvents -->
```cs
// Process after delay
context.Operation.AddEvent(new ReminderEvent(userId))
    .SetDelayBy(TimeSpan.FromHours(24));

// Process at specific time
context.Operation.AddEvent(new ScheduledEvent())
    .SetDelayUntil(scheduledTime);

// Rate-limited (one per minute)
context.Operation.AddEvent(new RateLimitedEvent())
    .SetDelayUntil(now, TimeSpan.FromMinutes(1), "rate-limit");
// ... the same, on a lattice aligned to the quantum rather than offset by the prefix's hash
context.Operation.AddEvent(new RateLimitedEvent())
    .SetDelayUntil(now, TimeSpan.FromMinutes(1), TimeSpan.Zero, "rate-limit");
```
<!-- endSnippet -->

### Event Conflict Strategies

<!-- snippet: PartOCS_EventConflictStrategies -->
```cs
// Skip duplicates (idempotent)
context.Operation.AddEvent(new NotifyEvent(userId))
    .SetUuid($"notify-{userId}-{DateTime.UtcNow:yyyy-MM-dd-HH}")
    .SetUuidConflictStrategy(KeyConflictStrategy.Skip);

// Fail on duplicate (default)
context.Operation.AddEvent(new UniqueEvent())
    .SetUuidConflictStrategy(KeyConflictStrategy.Fail);

// Update existing
context.Operation.AddEvent(new UpdatableEvent())
    .SetUuidConflictStrategy(KeyConflictStrategy.Update);
```
<!-- endSnippet -->

## Configuration Quick Reference

### Operation Log Reader

<!-- snippet: PartOCS_OperationLogReaderConfig -->
```cs
operations.ConfigureOperationLogReader(_ => new() {
    StartOffset = TimeSpan.FromSeconds(3),     // Startup lookback
    CheckPeriod = TimeSpan.FromSeconds(5),     // Poll interval
    BatchSize = 64,                            // Ops per batch
    ConcurrencyLevel = Environment.ProcessorCount * 4,
});
```
<!-- endSnippet -->

`StartOffset` positions a fresh reader at the earliest log entry logged within that window of "now" —
it deliberately skips older operations, which is fine because a fresh process has no old in-memory
computed values to invalidate. This relies on a few assumptions:

- Clock drift between hosts must stay well below `StartOffset` &ndash; a host whose clock is ahead can
  cause the reader to skip entries a slower-clocked host is still about to commit.
- A reader must establish its starting position within `StartOffset` of the moment the host starts
  serving compute calls; a slow startup path that serves calls before the reader is positioned can leave
  a gap.
- Dynamically introducing a shard into an already-running host, or a persistent remote-computed cache
  participating across restarts, are edge cases outside the "fresh process" assumption and deserve extra
  care.

None of this is a durable per-host/per-shard cursor &ndash; that would give stronger ordering and
recovery guarantees, at the cost of checkpoint lifecycle and host-identity management, and remains a
possible future enhancement rather than something implemented today.

### Operation Log Trimmer

<!-- snippet: PartOCS_OperationLogTrimmerConfig -->
```cs
operations.ConfigureOperationLogTrimmer(_ => new() {
    MaxEntryAge = TimeSpan.FromMinutes(30),    // 30 min default
    CheckPeriod = TimeSpan.FromMinutes(15),
});
```
<!-- endSnippet -->

### Operation Scope

<!-- snippet: PartOCS_OperationScopeConfig -->
```cs
operations.ConfigureOperationScope(_ => new() {
    IsolationLevel = System.Data.IsolationLevel.ReadCommitted,
});
```
<!-- endSnippet -->

### Event Log Reader

<!-- snippet: PartOCS_EventLogReaderConfig -->
```cs
operations.ConfigureEventLogReader(_ => new() {
    CheckPeriod = TimeSpan.FromSeconds(5),
    BatchSize = 64,
    ConcurrencyLevel = Environment.ProcessorCount * 4,
});
```
<!-- endSnippet -->

### Event Log Trimmer

<!-- snippet: PartOCS_EventLogTrimmerConfig -->
```cs
operations.ConfigureEventLogTrimmer(_ => new() {
    MaxEntryAge = TimeSpan.FromHours(1),       // 1 hour default
    CheckPeriod = TimeSpan.FromMinutes(15),
});
```
<!-- endSnippet -->

### Operation Reprocessor

<!-- snippet: PartOCS_OperationReprocessorConfig -->
```cs
fusion.AddOperationReprocessor(_ => new() {
    MaxRetryCount = 3,                         // Retry attempts
    RetryDelays = RetryDelaySeq.Exp(0.5, 3, 0.33),  // Exponential backoff
});
```
<!-- endSnippet -->

## Log Watchers

| Watcher | Method | Best For |
|---------|--------|----------|
| PostgreSQL | `AddNpgsqlOperationLogWatcher()` | PostgreSQL deployments |
| Redis | `AddRedisOperationLogWatcher()` | Any DB with Redis |
| File System | `AddFileSystemOperationLogWatcher()` | Local development |
| None | (default) | Polling fallback |

## Command Types

<!-- snippet: PartOCS_CommandTypes -->
```cs
// Standard command
public record CreateOrderCommand(long UserId) : ICommand<Order>
{
    public long OrderId { get; init; }
    public bool StatusChanged { get; init; }
    public string OldStatus { get; init; } = "";
}

// Backend-only command (server-side execution enforced)
public record DeleteUserCommand(long UserId) : ICommand<Unit>, IBackendCommand;

// Command with validation
public record UpdateProfileCommand(long UserId, string Name)
    : ICommand<Unit>, IPreparedCommand
{
    public Task Prepare(CommandContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Name is required");
        return Task.CompletedTask;
    }
}
```
<!-- endSnippet -->

## Transient vs Persistent Operations

| Aspect | Transient Operation | Persistent Operation |
|--------|---------------------|----------------------|
| Stored | No | Yes |
| Cross-host | No | Yes |
| Events | Not allowed | Allowed |
| UUID | `xxx-local` | `xxx` |

## Pipeline Priorities

| Priority | Handler | Purpose |
|----------|---------|---------|
| 999,999,000 | `InvalidationGuard` | Rejects commands run while invalidating |
| 100,000 | `OperationReprocessor` | Transient error retry |
| 10,000 | `TransientOperationScopeProvider` | Transient scope |
| 9,900 | `DbOperationScopeProvider` | DB scope |
| 9,000 | `DeferredInvalidationScopeProvider` | Deferred invalidation scope |
| -1,000,000,000 | `CompletionTerminator` | Terminal handler for `ICompletion` |

## Common Patterns

### Conditional Invalidation

<!-- snippet: PartOCS_ConditionalInvalidation -->
```cs
Invalidation.Defer(() => {
    _ = GetOrder(command.OrderId, default);
    if (command.StatusChanged)
        _ = GetOrdersByStatus(command.OldStatus, default);
});
```
<!-- endSnippet -->

### Multiple Invalidations

<!-- snippet: PartOCS_MultipleInvalidations -->
```cs
// Defer() may be called any number of times - the blocks run in registration order
Invalidation.Defer(() => _ = GetOrder(command.OrderId, default));
Invalidation.Defer(() => {
    _ = GetOrderList(command.UserId, default);
    _ = GetOrderCount(command.UserId, default);
});
```
<!-- endSnippet -->

### Nested Commands

<!-- snippet: PartOCS_NestedCommands -->
```cs
// The nested command declares its own invalidation, deferred into the same operation
await Commander.Call(new ChildCommand(parentId), cancellationToken);
```
<!-- endSnippet -->

### Control Operation Storage

<!-- snippet: PartOCS_ControlOperationStorage -->
```cs
// Disable storage (operation won't replicate)
context.Operation.StoreMode = OperationStoreMode.None;
```
<!-- endSnippet -->
