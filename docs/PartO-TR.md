# Operations Framework: Transient Operations

A **transient operation** is one that exists only in memory during command execution.
It's not written to the operation log, so its invalidation never reaches other hosts.

Transient operations are created by `TransientOperationScope` and have:
- `IsTransient = true`
- `HasStoredOperation = false`
- UUID format: `{uuid}-local`


## When Are Operations Transient?

Operations become transient when:
1. The command doesn't use `CreateOperationDbContext()` (no database interaction)
2. The command explicitly disables storage with `Operation.StoreMode = OperationStoreMode.None`

<!-- snippet: PartOTR_TransientOperation -->
```cs
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
```
<!-- endSnippet -->


## Persistent Operations

Operations become persistent when using `CreateOperationDbContext`:

<!-- snippet: PartOTR_PersistentOperation -->
```cs
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
```
<!-- endSnippet -->


## Comparison

| Aspect | Transient | Persistent |
|--------|-----------|------------|
| Scope Provider | `TransientOperationScopeProvider` | `DbOperationScopeProvider<T>` |
| `IsTransient` | `true` | `false` |
| Stored in DB | No | Yes |
| Invalidation reaches other hosts | No | Yes |
| Can have events | No | Yes |
| Can carry recorded invalidation calls | No | Yes |
| UUID suffix | `-local` | (none) |


## Controlling Storage

You can explicitly control whether an operation is stored:

<!-- snippet: PartOTR_ControlStorage -->
```cs
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
```
<!-- endSnippet -->


## Transient Operation Limitations

1. **No events**: Calling `AddEvent()` throws `TransientScopeOperationCannotHaveEvents`
2. **No cross-host invalidation**: Only the local host sees the invalidation
3. **Invalidation is local**: `Replicated` and `Distributed` both throw on a transient scope,
   because there's no stored row to carry the recorded calls

Use transient operations for:
- Commands that modify only in-memory state
- Commands that don't need cluster-wide invalidation
- Commands where you handle invalidation through other means


## Operation Completion

Whether transient or persistent, all operations go through **completion**:

<img src="/img/diagrams/PartO-TR-1.svg" alt="Operation Completion" style="width: 100%; max-width: 800px;" />

The completion flow is the same for both transient and persistent operations, but only a persistent
operation's recorded invalidation calls reach other hosts.
