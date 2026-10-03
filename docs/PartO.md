# Operations Framework: Multi-Host Invalidation, CQRS, and Reliable Command Processing

The Operations Framework (OF) provides a robust foundation for building distributed systems with Fusion.
It solves several critical challenges that arise when running multiple instances of an application:

- **Multi-host cache invalidation**: When data changes on one server, all other servers must invalidate
  their cached computed values
- **Reliable command processing**: Commands must be executed exactly once, even in the face of failures
- **Event-driven architecture**: Commands can produce events that are processed asynchronously with
  guaranteed delivery

## Why Do You Need Operations Framework?

Consider a typical multi-server deployment:

<img src="/img/diagrams/PartO-1.svg" alt="Why Do You Need Operations Framework?" style="width: 100%; max-width: 800px;" />

When a user on Server A updates their profile:
1. Server A writes to the database and invalidates its local cache
2. Servers B and C still have stale data in their caches
3. Users connected to B and C see outdated information

**Without Operations Framework**, you'd have to implement:
- A message queue or pub/sub system for cross-server notifications
- Retry logic for failed operations
- Deduplication to prevent processing the same operation twice
- Transaction handling to ensure atomicity

**With Operations Framework**, all of this is handled automatically.

## Required Packages

| Package | Purpose |
|---------|---------|
| [ActualLab.Fusion](https://www.nuget.org/packages/ActualLab.Fusion/) | Core OF abstractions and in-memory implementation |
| [ActualLab.Fusion.EntityFramework](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework/) | EF Core implementation: `DbOperationScope`, operation logging, `DbContext` integration |
| [ActualLab.Fusion.EntityFramework.Npgsql](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework.Npgsql/) | PostgreSQL: `NpgsqlOperationLogWatcher` for LISTEN/NOTIFY |
| [ActualLab.Fusion.EntityFramework.Redis](https://www.nuget.org/packages/ActualLab.Fusion.EntityFramework.Redis/) | Redis: `RedisOperationLogWatcher` for pub/sub notifications |

::: tip
The base `ActualLab.Fusion.EntityFramework` package includes `FileSystemOperationLogWatcher` which works with any database but requires shared filesystem access. For production multi-host deployments, use database-specific watchers (Npgsql) or Redis.
:::

## The Outbox Pattern

Operations Framework implements the **Transactional Outbox Pattern** &ndash; a well-known solution
for reliable messaging in distributed systems.

### The Problem

In distributed systems, you often need to:
1. Update your database
2. Publish a message/event to notify other services

But what if step 2 fails after step 1 succeeds? You have inconsistent state.

### The Solution: Outbox Pattern

Instead of publishing directly, write the message to an "outbox" table in the **same transaction**
as your business data:

<img src="/img/diagrams/PartO-2.svg" alt="The Solution: Outbox Pattern" style="width: 100%; max-width: 800px;" />

This guarantees **at-least-once delivery**: if the transaction commits, the operation will
eventually be processed. If it fails, nothing is written.

### How OF Implements It

1. **DbOperationScope** wraps your command in a database transaction
2. **DbOperation** entity stores the operation in the same transaction
3. **DbOperationLogReader** (background service) watches for new operations
4. **Operation Log Watchers** provide instant notifications (PostgreSQL NOTIFY, Redis Pub/Sub, etc.)
5. **OperationCompletionNotifier** triggers invalidation on all hosts

## Core Concepts

### Operation

An **Operation** is the durable record of a completed command: the command itself, the invalidation
calls it recorded, and the events it produced. Other hosts read that record and apply those calls -
nothing is replayed. Currently only commands act as operations, but the framework is designed to
support other types in the future.

Key properties:
- `Uuid` &ndash; Unique identifier
- `HostId` &ndash; The server that executed the operation
- `Command` &ndash; The command that was executed
- `InvalidationCalls` &ndash; The calls applied after this operation completes, on every host that reads it
- `Events` &ndash; Events produced by this operation

Both lists are immutable, and both can be shaped from a handler before the commit:

| Member | What it does |
|---|---|
| `AddInvalidationCall(call)` | Appends one recorded call; returns the operation, so calls chain |
| `AddInvalidationCalls(...)` | Appends several &ndash; `params ReadOnlySpan<ServiceCall>` or an `IEnumerable<ServiceCall>` |
| `RemoveInvalidationCall(call)` | Removes that call, returning whether it was there |
| `RemoveInvalidationCalls()` | Drops all of them |
| `RemoveInvalidationCalls(predicate)` | Drops the ones the predicate matches |
| `AddEvent(...)` | Appends an event &ndash; from a value, a uuid and a value, an `IOperationEventSource`, or an `OperationEvent` |
| `RemoveEvent(event)` / `RemoveEvent(uuid)` | Removes one event, returning whether it was there |
| `RemoveEvents()` / `RemoveEvents(predicate)` | Drops all events, or the matching ones |

You rarely need these: `Invalidation.Defer(...)` is what records invalidation calls, and
`AddEvent(...)` is the normal way to raise an event. They exist for the cases where a handler wants
to inspect or edit what it is about to commit &ndash; dropping an invalidation a nested command
already covered, say.

A `ServiceCall` compares by reference rather than by content, so `RemoveInvalidationCall` removes
the instance you hand it, not an equal-looking one. Use
`ServiceCall.ContentEqualityComparer.Instance` to compare two calls by what they invoke.

The event members need an active, non-transient scope and throw otherwise &ndash; a transient
operation cannot carry events. The invalidation-call members have no such requirement: a recorded
call is just data.

### Operation Scope

An **Operation Scope** provides the context for operation execution:

- **DbOperationScope**: Persistent operations stored in database (default for database commands)
- **TransientOperationScope**: Transient operations that don't persist (for in-memory commands)

### Deferred Invalidation Mode

A handler declares what its mutation invalidated by calling `Invalidation.Defer(...)`, which
registers a block to run after the mutation commits. The handler's **invalidation mode** decides
how far those blocks reach: the origin host only (`Local`), every host (`Replicated`), or the host
that owns each value (`Distributed`). There is no default &ndash; a handler that defers a block must
carry `[DeferredInvalidationMode(...)]`, or the call throws. See
[Invalidation Modes](./PartO-IM.md).

## Quick Start

### 1. Add DbSet for Operations

<!-- snippet: PartO_DbSet -->
```cs
public DbSet<DbOperation> Operations { get; protected set; } = null!;
public DbSet<DbEvent> Events { get; protected set; } = null!;
```
<!-- endSnippet -->

### 2. Configure Services

<!-- snippet: PartO_AddDbContextServices -->
```cs
public static void ConfigureServices(IServiceCollection services, IHostEnvironment Env)
{
    services.AddDbContextServices<AppDbContext>(db => {
        // Uncomment if you'll be using AddRedisOperationLogWatcher
        // db.AddRedisDb("127.0.0.1", "FusionDocumentation.PartO");

        db.AddOperations(operations => {
            // This call enabled Operations Framework (OF) for AppDbContext.
            operations.ConfigureOperationLogReader(_ => new() {
                // We use AddFileSystemOperationLogWatcher, so unconditional wake up period
                // can be arbitrary long – all depends on the reliability of Notifier-Monitor chain.
                // See what .ToRandom does – most of timeouts in Fusion settings are RandomTimeSpan-s,
                // but you can provide a normal one too – there is an implicit conversion from it.
                CheckPeriod = TimeSpan.FromSeconds(Env.IsDevelopment() ? 60 : 5).ToRandom(0.05),
            });
            // Optionally enable file-based operation log watcher
            operations.AddFileSystemOperationLogWatcher();

            // Or, if you use PostgreSQL, use this instead of above line
            // operations.AddNpgsqlOperationLogWatcher();

            // Or, if you use Redis, use this instead of above line
            // operations.AddRedisOperationLogWatcher();
        });
    });
}
```
<!-- endSnippet -->

> Note: OF works solely on the server side, so you don't need similar configuration
> in your Blazor WebAssembly client.

### 3. Create Command and Handler

<!-- snippet: PartO_PostMessageCommand -->
```cs
public record PostMessageCommand(Session Session, string Text) : ICommand<ChatMessage>;
```
<!-- endSnippet -->

<!-- snippet: PartO_PostOfHandler -->
```cs
[CommandHandler]
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public virtual async Task<ChatMessage> PostMessage(
    PostMessageCommand command, CancellationToken cancellationToken = default)
{
    await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
    // Actual code...
    var message = await PostMessageImpl(dbContext, command, cancellationToken);

    Invalidation.Defer(() => _ = PseudoGetAnyChatTail());
    return message;
}
```
<!-- endSnippet -->

## Command Handler Structure

A command handler with Operations Framework follows this pattern:

```cs
[CommandHandler]
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public virtual async Task<TResult> HandleCommand(
    TCommand command, CancellationToken cancellationToken = default)
{
    // 1. MUTATE
    await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);

    // Perform your business logic
    var result = await DoWork(dbContext, command, cancellationToken);

    await dbContext.SaveChangesAsync(cancellationToken);

    // 2. DECLARE THE INVALIDATION - the block runs after the commit
    Invalidation.Defer(() => {
        _ = GetSomeData(command.Id, default);
        _ = GetRelatedData(command.RelatedId, default);
    });
    return result;
}
```

### Key Points

1. **`virtual` modifier** &ndash; Required for Fusion's proxy generation
2. **`[CommandHandler]` attribute** &ndash; Registers this method as a command handler
3. **`CreateOperationDbContext`** &ndash; Creates a DbContext that participates in the operation scope
4. **`Invalidation.Defer(...)`** &ndash; May be called any number of times, anywhere in the handler;
   the blocks run in registration order, after the mutation commits, on `CancellationToken.None`

::: warning Don't spawn background work from inside an invalidation block
A deferred block (and any synchronous `Invalidated` event handler) runs inside an
`Invalidation.Begin()` scope. That scope is `AsyncLocal`-based, so if you start a `Task.Run` or similar
background work from within it, the spawned work inherits the same ambient invalidation context even
after the block exits. Any compute method it calls will silently skip computation and invalidate instead
&ndash; see [`Computed.BeginIsolation()`](./PartF-C.md#context-scopes) for the guardrail.
:::

## Conditional Invalidation

What to invalidate often depends on what the mutation discovered. A deferred block is an ordinary
closure, so that's just a captured local:

<!-- snippet: PartO_SignOutHandler -->
```cs
[CommandHandler]
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public virtual async Task SignOut(
    SignOutCommand command, CancellationToken cancellationToken = default)
{
    await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);

    var dbSessionInfo = await Sessions.FindOrCreate(dbContext, command.Session, cancellationToken).ConfigureAwait(false);
    var sessionInfo = dbSessionInfo.ToModel();
    if (sessionInfo.IsSignOutForced)
        return;

    // ...

    // What to invalidate depends on what the mutation found, so it's an ordinary closure
    Invalidation.Defer(() => {
        _ = GetUser(sessionInfo.UserId, default);
        _ = GetUserSessions(sessionInfo.UserId, default);
    });
}
```
<!-- endSnippet -->

A block observes the *final* value of a captured local, not its value at the `Defer(...)` call
site &ndash; ordinary closure semantics, but easy to trip over when the call reads as if it runs in
place. Snapshot into a fresh local if you need the earlier value.

## Testing Invalidation

Dependency propagation invalidates every *transitive* dependant of a call automatically, but it can't
invent an invalidation for a *directly affected* call that a handler's deferred block forgot to
enumerate. Nothing catches that at compile time &ndash; a query added later, a changed data dependency,
or a broad aggregate query that isn't obviously "related" to the command can silently fall out of sync
with reality.

The mitigation isn't an analyzer (the design space is too broad for one to pay off yet) &ndash; it's a
test convention: **for every mutating command, write a test that proves it invalidates both the
entity-specific query it targets and any aggregate query whose result depends on the same data.**

<!-- snippet: PartO_TestInvalidation_Command -->
```cs
public record KeyValueService_Set(string Key, string Value) : ICommand<Unit>;
```
<!-- endSnippet -->

<!-- snippet: PartO_TestInvalidation_Service -->
```cs
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class KeyValueService : IComputeService
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    // Entity-specific query
    [ComputeMethod]
    public virtual Task<string?> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    // Aggregate query
    [ComputeMethod]
    public virtual Task<int> Count(CancellationToken cancellationToken = default)
        => Task.FromResult(_values.Count);

    [CommandHandler]
    public virtual Task<Unit> Set(KeyValueService_Set command, CancellationToken cancellationToken = default)
    {
        // Requests an operation scope, so this in-memory command commits like a stored
        // one does -- see TransientOperationScopeProvider
        TransientOperationScope.Require();
        _values[command.Key] = command.Value;

        // Every mutating command handler must invalidate BOTH the entity-specific
        // query it directly affects AND every aggregate query whose result may change --
        // dependency tracking alone won't discover an omitted root call.
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = Count(default);
        });
        return Task.FromResult(Unit.Default);
    }
}
```
<!-- endSnippet -->

Capture both computed values *before* calling the command, then assert both got invalidated:

<!-- snippet: PartO_TestInvalidation_Demo -->
```cs
var services = new ServiceCollection();
services.AddFusion().AddComputeService<KeyValueService>();
var sp = services.BuildServiceProvider();
var commander = sp.Commander();
var kv = sp.GetRequiredService<KeyValueService>();

// Capture both queries BEFORE the mutating command runs
var cGet = await Computed.Capture(() => kv.Get("a"));
var cCount = await Computed.Capture(() => kv.Count());
WriteLine($"Before Set: Get.IsConsistent={cGet.IsConsistent()}, Count.IsConsistent={cCount.IsConsistent()}");

await commander.Call(new KeyValueService_Set("a", "1"));

// A test would assert both are False here -- if Set() only invalidated
// Get(), this would (correctly) fail and catch the missing Count() invalidation
WriteLine($"After Set:  Get.IsConsistent={cGet.IsConsistent()}, Count.IsConsistent={cCount.IsConsistent()}");
```
<!-- endSnippet -->

The output:

```text
Before Set: Get.IsConsistent=True, Count.IsConsistent=True
After Set:  Get.IsConsistent=False, Count.IsConsistent=False
```

If `Set`'s deferred block only called `Get(command.Key, default)` and forgot `Count(default)`, this
test would catch it immediately: `Count.IsConsistent` would stay `True` after the command completes.
Without the test, that gap would surface later as a UI aggregate (a count, a list, a total) that never
updates, with nothing in the logs pointing at the cause.

## Nested Commands

When one command calls another, the nested handler's deferred blocks join the same operation:

```cs
[CommandHandler]
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public virtual async Task<Order> CreateOrder(
    CreateOrderCommand command, CancellationToken cancellationToken = default)
{
    await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);

    var order = new Order { /* ... */ };
    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync(cancellationToken);

    // The nested command declares its own invalidation, deferred into this operation
    await Commander.Call(new SendOrderConfirmationCommand(order.Id), cancellationToken);

    Invalidation.Defer(() => _ = GetOrder(command.OrderId, default));
    return order;
}
```

One operation gets one capture scope, and its mode comes from the **first handler that defers a
block** &ndash; not from the outermost command. A handler that defers nothing never gets a say, and
two handlers that need different modes fail rather than one of them being silently narrowed &ndash;
see [Nested Commands](./PartO-IM.md#nested-commands).

## Command Pipeline

Operations Framework adds several filtering handlers to the command pipeline:

| Priority | Handler | Purpose |
|----------|---------|---------|
| 999,999,000 | `InvalidationGuard` | Throws if a command is started inside an invalidation pass |
| 100,000 | `OperationReprocessor` | Retries commands that fail with transient errors |
| 10,000 | `TransientOperationScopeProvider` | Provides transient scope, runs completion |
| 9,900 | `DbOperationScopeProvider` | Provides database scope for each DbContext type |
| 9,000 | `DeferredInvalidationScopeProvider` | Opens the deferred invalidation scope |
| -1,000,000,000 | `CompletionTerminator` | Terminal handler for `ICompletion` |

## Invariants and Guarantees

### Invalidation blocks and completion listeners must not fail

Deferred invalidation and `OperationCompletionNotifier`'s dispatch to
`IOperationCompletionListener`s both catch every exception, log it, and otherwise treat the operation as
fully processed &ndash; a failed invalidation block or a failed listener is **not** retried, and (on the
originating host) it's never revisited within the same process lifetime. The mutation has already
committed by then, so failing the command isn't an option either.

This is a deliberate design trade-off, but it rests on a hard, mostly-unenforced contract: **invalidation
logic and completion listeners must not fail.** Concretely, that means:

- Deferred blocks and any compute method they transitively call must be synchronous or otherwise
  guaranteed to complete, side-effect-free, and independent of failure-prone infrastructure (no I/O
  that can legitimately fail).
- Custom `IOperationCompletionListener` implementations must not throw under any input they can
  reasonably observe.

If this contract is violated, the swallowed exception is your only signal, and the corresponding
invalidation (or completion notification) is simply lost for that operation. When writing either kind of
code, test it explicitly against failure scenarios you'd otherwise rely on retry semantics to paper over
&ndash; retries are not coming. The `invalidation.deferred.failure.count` counter is what surfaces a
block that threw.

### Completion listener delivery is at-least-once

For operations delivered by the log reader (i.e. everything except the originating host's in-process
notification), `IOperationCompletionListener` dispatch is **at-least-once**, not exactly-once. When a
listener fails, the reader redelivers the whole operation; on a crash&ndash;restart it also re-reads
every operation still within `StartOffset`. Redelivery re-runs **all** listeners registered for that
operation &ndash; including the ones that already succeeded on the previous attempt, since the reader
tracks progress per operation, not per listener. **Listeners with external side effects must therefore
be idempotent**, keyed off `Operation.Uuid` (or an equivalent per-event key) so a re-run is a no-op.

### Command completion isn't a cluster-wide freshness boundary

After a command's transaction commits, invalidation still has to pass through local completion handling,
watcher notification (or the poll fallback), application of the operation's recorded invalidation
calls on every other host, and &ndash; for
RPC clients &ndash; a further `Invalidate` message. During that interval, another host or a connected
client can legitimately observe a cached pre-command value. This is intentional eventual consistency, not
a bug: **`Commander.Call` returning does not mean every dependent cache cluster-wide has been
invalidated.**

If a caller needs cross-host read-your-write semantics (e.g., "the very next read on any host must see
this write"), command completion alone doesn't provide it &ndash; that requires an explicit version or
synchronization mechanism layered on top (e.g., threading a version/timestamp through the response and
having the reader wait for a computed at least that fresh).

## Backend Commands

Commands that should only execute on the server should implement `IBackendCommand`:

```cs
public record DeleteUserCommand(UserId UserId) : ICommand<Unit>, IBackendCommand;
```

This ensures:
- The command can only be processed by backend servers
- Client-side proxies won't attempt to handle it
- RPC layer enforces server-side execution

## Further Reading

- [Invalidation Modes](./PartO-IM.md) &ndash; Deferred, replicated, and distributed invalidation
- [Events](./PartO-EV.md) &ndash; Producing and consuming events from operations
- [Transient Operations and Reprocessing](./PartO-TR.md) &ndash; In-memory operations and retry logic
- [Configuration Options](./PartO-CO.md) &ndash; All configuration options explained
- [Log Watchers](./PartO-PR.md) &ndash; PostgreSQL, Redis, FileSystem log watchers
- [Diagrams](./PartO-D.md) &ndash; Visual representations of OF internals
- [Cheat Sheet](./PartO-CS.md) &ndash; Quick reference

## Learning More

To explore OF's internals, check out:

- [`DbContextBuilder.AddOperations`](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion.EntityFramework/DbOperationsBuilder.cs)
- [`FusionBuilder`](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion/FusionBuilder.cs)

### HostId

`HostId` identifies each process in your cluster. It includes:
- Machine name
- Unique process ID
- Unique ID per IoC container (useful for testing)

This allows OF to determine if an operation originated locally or from a peer.

### OperationCompletionHandler

`OperationCompletionHandler` applies the invalidation calls an operation recorded &ndash;
locally right after the commit, and on every other host once the operation log delivers the
operation. A call whose service isn't registered on the applying host is dropped by design, which
is what makes a pure RPC client a no-op here: the host that owns the service replicates its own
invalidations.

It is also the extension point for anything else you want to happen on completion. Register your
own through `CommanderBuilder.AddOperationCompletionHandler`, which is how `AddFusion()` swaps
CommandR's base handler for `FusionOperationCompletionHandler`:

```cs
commander.AddOperationCompletionHandler(c => new MyCompletionHandler(c));
```

One registration call wires all four things an operation's completion needs &ndash; the DI
registration, the `IOperationCompletionListener` entry, the `OperationCompletion` command handler,
and the alias that makes `OperationCompletionHandler` resolve to your type &ndash; and it *replaces*
whatever was registered before, because a completion applied twice would invalidate twice.

Derive from `FusionOperationCompletionHandler` (not from the CommandR base) in a Fusion app, and
override whichever of these you need:

| Member | When it runs |
|---|---|
| `OnOperationCompleted(operation, commandContext)` | Every completed operation, local or read from the log |
| `OnCommand(OperationCompletion, ...)` | A routed or recovered completion arriving as a command |
| `ApplyInvalidations(calls, handleLocally, ...)` | Applies a whole batch, locally or by routing |
| `ApplyInvalidation(call, ...)` | One call |
| `DropInvalidation(call, reason, ...)` | A call that couldn't be applied &ndash; the hook for logging or metrics |

An `ICompletion<TCommand>` handler is the other way in, and the better one when what you want is
tied to a specific command rather than to invalidation. See [Backend Commands](#backend-commands).
Note that such a handler has to be a **filter**: `CompletionTerminator` is the one non-filter
handler of every completion, and a second one is an error.

## Getting Help

If you run into issues, join [Fusion Place](https://voxt.ai/chat/s-1KCdcYy9z2-uJVPKZsbEo)
and ask questions. The author (Alex Y.) is active and happy to help.
