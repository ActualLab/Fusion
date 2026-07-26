# Deferred Invalidation: one authoring form for local and distributed invalidation

## Goal

Today a mutation declares what it invalidates in one of two mutually incompatible
ways, and which one you must use is decided by how the service is deployed:

- **Replicated services** (shared DB, every host computes locally) use the
  `if (Invalidation.IsActive) { … return; }` guard. The command is replayed on
  every host by `InvalidatingCommandCompletionHandler`, so everything the
  invalidation pass needs must be marshalled through `Operation.Items`.
- **Shard-owned services** (single owner per key, RPC-routed) use inline
  `using (Invalidation.Begin()) _ = Get(key, default);`. No replay, no `Items`,
  no completion pipeline — local invalidation is sufficient because there is only
  one authoritative host per key.

The two forms are not interchangeable, so a service cannot move between the two
deployment models without being rewritten.

**Goal:** one authoring form, with the local-vs-distributed choice made by
configuration. Concretely: `Invalidation.Defer()` replaces both, and the
mutation body is never executed twice.

## Background: what the two worlds look like

### Replicated

`ContactsBackend`, `ChatsBackend`, `AccountsBackend`, … — see
`ContactsBackend.OnChange` for the canonical shape:

```csharp
// [CommandHandler]
public virtual async Task<Contact?> OnChange(ContactsBackend_Change command, CancellationToken cancellationToken)
{
    var context = CommandContext.GetCurrent();
    if (Invalidation.IsActive) {
        var invIndex = context.Operation.Items.KeylessGet(long.MinValue);
        if (invIndex != long.MinValue) {
            _ = Get(ownerId, id, default);
            _ = ListIds(ownerId, placeId, default);
        }
        return default!;
    }

    ... mutate DB ...
    context.Operation.Items.KeylessSet(...);
}
```

Pipeline: `DbOperationScope` → operation log row → `OperationCompletionNotifier`
→ `CompletionProducer` → `ICompletion` → `InvalidatingCommandCompletionHandler`,
which re-invokes the final handler under `Invalidation.Begin()` on every host.

### Shard-owned

`LiveSessionsBackend`, `LiveVideoBackend`, `LiveAudioBackend` (all
`ShardComputeService` on `ShardScheme.LiveBackend`), plus `UserPresencesBackend`
in spirit. Mutators are plain RPC methods, not commands; compute methods gate on
`ShardOwner.RequireShardOwnership(key, addDependency: true, ct)`, and
`MeshRpcRoute` routes every call for a key to the single owner.

```csharp
public virtual async Task SetParticipation(ChatId chatId, AuthorId authorId, …)
{
    using (Computed.BeginIsolation())
    using (await _changeLocks.Lock(chatId, cancellationToken).ConfigureAwait(false)) {
        await _participants.Set(...).ConfigureAwait(false);
        InvalidateListParticipants(chatId);
        InvalidateHasRecorder(chatId);
        InvalidateGet(chatId);
        emptiedByLeave = !isActive && !await IsSessionLive(chatId).ConfigureAwait(false);
        ...
    }
}
```

Two properties of this style matter for the design:

1. Invalidation is **scattered and conditional** — inside `if`s, loops, and helper
   methods (`EvaluateLiveness`, `ExpireRings`). A single guarded block at the top
   of the method can only reproduce that by encoding every condition as an `Items`
   flag for the replay to re-read.
2. Invalidation is **immediate, and read back within the same method**:
   `SetParticipation` invalidates `ListParticipants`, then calls `IsSessionLive`
   and expects the recomputed value. Deferring this unconditionally would break it
   silently.

## The protocol

Instead of shipping *the command to replay*, record and ship *the invalidation itself*.

Fusion already has a canonical description of "this exact computed":
`ComputeMethodInput` = `ComputeMethodDef` + `Invocation.Arguments`. That is the
same tuple RPC serializes for every outbound compute call, so compute-method
arguments are serializable by construction. Capturing it during the mutation gives
a replayable **invalidation call**.

### `Defer` takes a delegate, not a `using` block

```csharp
public static class Invalidation
{
    // Opens a capture scope. The handler decides what happens to the collected
    // delegates and when; the default one runs them at scope exit.
    public static DeferInvalidationScopeHandle BeginDeferred(IDeferInvalidationHandler? handler = null);

    public static void Defer(Action action);
    public static void Defer(Func<Task> action);
}
```

The delegates always run on `CancellationToken.None`, and the `Defer` overloads take no
token. They run *after* the mutation committed, so inheriting the caller's token would
mean an aborted HTTP request or a dropped RPC peer silently skips the invalidation —
the write lands and no cache hears about it. `DbOperationScope.VerifyCommit` already
runs on `None` for the same reason. Host shutdown is ignored too: these blocks are
microseconds, and skipping them leaves stale caches behind on the surviving hosts.

A `using` block would execute at its location, which forces recording to happen there
and only there. A **delegate defers the block itself**, and that is what lets one piece
of source serve both deferred modes:

| Mode | What happens to the delegate |
|---|---|
| `Local` | Nothing is recorded. Once the commit is verified, it runs inside `Invalidation.Begin()` — in-process, on the one host that owns the key. |
| `Replicated` | It runs at commit under the recording context (`CallOptions.DeferInvalidate`) to harvest `InvalidationCall` entries, which go into the operation record and ship to every host. |

The consequence is large: **the `Local` path needs none of the recording machinery** —
no `InvalidationCall`, no `ArgumentList` capture, no method identity, no
`ComputeServiceRegistry`, no argument serialization, and therefore no version-skew
surface at all. It only needs to hold a delegate and run it after commit. All of that
apparatus exists solely to serve `Replicated`.

**Placement is unconstrained, and `Defer(...)` may be called any number of times** —
including inside `if` branches and loops, which is the natural way to express
conditional invalidation. The delegates run in registration order.

The two timings are equivalent, and not by luck: `Replicated` harvests as the first step
of `IOperationScope.Commit()`, and the scope providers call `Commit()` *after*
`context.InvokeRemainingHandlers(...)` returns; `Local` runs the delegates after that
commit. **Both therefore run once the handler body has already completed**, so a
captured local holds the same value either way. Flipping the mode cannot change
what a delegate sees.

The one semantic to know: because the delegate runs after the body, it observes the
**final** value of a captured local, not its value at the `Defer(...)` call site. If a
handler mutates a variable after deferring over it and wants the earlier value, it has
to snapshot into a fresh local — ordinary closure semantics, but easy to trip over when
the call reads like it executes in place.

`Defer()` is **defer or fail**: outside a capturing scope it throws, rather than
silently invalidating early. There is no `DeferOrBegin()` — the fallback it existed for
never triggers, because a scope is always open where `Defer()` is legal:
`InMemoryOperationScopeProvider` opens one for every command handler, and a non-command
mutator opens its own with `using var _ = Invalidation.BeginDeferred();`.

### What this buys

- No double execution of the handler.
- No `Operation.Items` marshalling for invalidation purposes: the condition is
  evaluated once, where the data already is, instead of being encoded as a flag
  and re-evaluated on every host.
- Conditional and looped invalidation is expressed as ordinary control flow.
- `Local` services get deferred invalidation with essentially zero new machinery.

### Rewrite examples

```csharp
// ContactsBackend.OnChange - after
// [CommandHandler]
[InvalidationMode(InvalidationMode.Local)]
public virtual async Task<Contact?> OnChange(ContactsBackend_Change command, CancellationToken cancellationToken)
{
    var (id, expectedVersion, change) = command;
    ... mutate DB ...
    Invalidation.Defer(() => {
        _ = Get(ownerId, id, default);
        if (index < 0 || index > Constants.Contacts.MinLoadLimit)
            _ = ListIds(ownerId, placeId, default);
    });
    return contact;
}
```

`UserPresencesBackend.OnCheckIn`'s
`context.Operation.AddCompletionHandler(scope => { using (Invalidation.Begin()) … })`
collapses to `Invalidation.Defer(() => _ = GetLastCheckIn(userId, default));` — which is
almost the same code, minus the completion-handler ceremony and minus the decision about
where the invalidation is applied.

### The case this was designed for

A shard-owned service that *does* use a DB: `CreateOperationDbContext` for the
transaction and its commit verification, `MustStore(false)` because no other host needs
to read the operation, and `Defer(...)` for invalidation that must run locally. Under
`Local` that combination now needs no op-log row, no completion command, and
no recorded calls — just "verify the commit landed, then run this delegate under
`Invalidation.Begin()`".

## The capture scope is not an `Operation`

An earlier draft of this plan said "give shard-service mutators an ambient ephemeral
operation, so `Defer()` has something to record into". That is the wrong dependency.
`Operation` currently bundles four separate things:

1. a **commit boundary** (the DB transaction),
2. a **commit verifier** (a uniquely-keyed row proving the transaction landed),
3. a **broadcast channel** (a `DbOperation` row that every host's log reader replays),
4. **post-commit side effects** — events, completion handlers, invalidation.

Requiring an `Operation` just to get (4) drags in (3), and (3) is precisely what a
shard-owned service does not want: "operation" today implies other hosts will read it.
That is why the live services avoid the Operations Framework entirely, and why
`UserPresencesBackend.OnCheckIn` reaches for `MustStore(false)` — it wants a
transaction without a log row.

So the capture is its own concept:

```csharp
/// <summary>
/// Collector of deferred invalidation blocks. What happens to them, and when,
/// is up to its <see cref="IDeferInvalidationHandler"/>.
/// </summary>
public sealed class DeferInvalidationScope
{
    private static readonly AsyncLocal<DeferInvalidationScope?> CurrentLocal = new();

    public static DeferInvalidationScope? Current => CurrentLocal.Value;

    public IDeferInvalidationHandler Handler { get; }
    public void Add(Func<Task> action);
    public Task Run();                        // runs the delegates under Invalidation.Begin()
    public Task<InvalidationCallSet> Harvest();   // runs them in recording mode
}

/// <summary>
/// Decides when a <see cref="DeferInvalidationScope"/>'s delegates are consumed,
/// and whether they are run or harvested.
/// </summary>
public interface IDeferInvalidationHandler
{
    Task OnScopeExit(DeferInvalidationScope scope, Exception? error);
}
```

The scope holds **delegates**, not calls, and the handler is the extension point.
The default handler runs them at scope exit unless an exception is in flight — that is
all a Redis-backed mutator needs, since "commit" there just means "returned without
throwing". OF supplies its own handler instead, which defers consumption to the
operation's commit and picks `Run` or `Harvest` by mode. That is the only place
the two deferred modes diverge, and OF plugs into it rather than the primitive knowing
anything about operations.

The ambient slot is **Fusion's own `AsyncLocal`, not a member on `CommandContext`**.
Two reasons, and the second is the important one:

1. `CommandContext` belongs to CommandR, which knows nothing about compute methods or
   invalidation; hanging a Fusion concept off it inverts the layering.
2. **The mechanism shouldn't be tied to CommandR at all.** Anyone can open a scope —
   `using var _ = DeferInvalidationScope.Begin();` — and flush it at their own commit
   boundary. Deferred invalidation is a Fusion concept that works with commands, not a
   command concept.

CommandR and the Operations Framework are then an **integration layer** on top:

- `InMemoryOperationScopeProvider` (a Fusion type, priority 10,000, the outermost
  operations filter) opens a scope around `context.InvokeRemainingHandlers(...)` and
  disposes it in `finally`, so `Defer()` works from a handler's first statement.
- `IOperationScope.Commit()` drains it into the operation — see below.

Nesting falls out of that provider's existing `isRequired` condition
(`context.IsOutermost && command is not ISystemCommand && !Invalidation.IsActive`):

- a nested command sharing the outer operation opens no scope and records into the
  outer one — matching how it already shares `NestedOperations`;
- an isolated nested command (`Commander.Call(cmd, true, ct)`, which creates its own
  outermost context) opens its own scope, shadows the outer, and restores on dispose —
  matching how it already gets its own operation scope.

**In practice, capture blocks live inside command handlers and are brief** — a few
statements, not long-lived ambient state. That is usage guidance rather than a
structural constraint, but it is what makes the per-call ambient lookup below a
non-issue, and it is why a shard-owned mutator will usually become a command handler
even though nothing in the mechanism forces it to.

The capture is still **not** the `Operation`: it exists wherever someone opened a
scope, whether or not an operation scope is ever created. When an operation does
exist, the calls migrate into it at commit.

## Commit verification and the three axes

Today one flag decides two unrelated things. In `DbOperationScope.Commit`:

```csharp
HasStoredOperation = MustStoreOperation;
var dbCommitVerifier = MustStoreOperation
    ? (object)new DbOperation(Operation)
    : new DbEvent(Operation, versionGenerator);
dbContext.Add(dbCommitVerifier);
```

`MustStoreOperation` picks **both** the verifier row kind **and** whether other hosts
replay this operation. `VerifyCommit` then looks the row up by `Uuid` in whichever
table it landed in, and the same `Uuid` uniqueness is what lets a reprocessed attempt
detect "already committed".

Under this design those are three independent axes:

| Axis | Question | Decided by |
|---|---|---|
| commit boundary | is there a transaction to commit? | the handler (does it touch a DB) |
| commit verification | can I prove the commit landed / dedupe a retry? | always on when there is a transaction |
| invalidation reach | do other hosts need to hear about this? | `InvalidationMode`, per service |

**Recommendation: keep the mechanism, split the flag.**

- Commit verification stays exactly as it is, including reusing `DbEvent` as the
  marker row when the operation isn't broadcast. It is slightly impure — non-events in
  the events table, which the event processor skips via the null-value check — but it
  is correct, it is already load-bearing, and the alternatives are worse:
  - a dedicated `DbCommitMarker` table adds a migration, a second uniqueness index,
    and another trimmer for no behavioral gain;
  - always writing a `DbOperation` with a `MustReplay` filter column is conceptually
    cleanest but grows the log-reader's table with every presence check-in — the exact
    cost `MustStore(false)` exists to avoid.
- `MustStoreOperation` gets renamed to what it now means — `MustBroadcastOperation` —
  and stops being hand-set by handlers. It is derived from the service's
  `InvalidationMode`, plus "this operation has events", plus "this operation carries a
  legacy replay payload". `UserPresencesBackend.OnCheckIn`'s manual
  `context.Operation.MustStore(false)` then simply disappears: it is implied by
  `IUserPresencesBackend` being `InvalidationMode.Local`.

**Invalidation calls should not travel as events.** Events are durable, ordered by
uuid, and processed at-least-once by a scheduler; invalidation is best-effort and
idempotent. For `Replicated` over a DB the calls belong in the `DbOperation` row, which
every host already reads. A service that needs a genuinely durable cross-host
notification keeps using events or the queue, exactly as
`LiveSessionsBackend.EnqueueLiveNotification` does today — that path is unchanged.

One consequence worth stating: `InMemoryOperationScope` forbids events outright
(`TransientScopeOperationCannotHaveEvents`), so "transient operation" is not a general
substitute for "operation without broadcast". With the axes split, the honest options
become: a command handler with no operation scope at all (shard service — the
`DeferInvalidationScope` still exists, since it belongs to the command context), an
operation that commits and verifies but doesn't broadcast, or a fully broadcast one.

## Implementation

### `InvalidationCall` — the recorded unit

```csharp
/// <summary>
/// A recorded compute-method call to invalidate: everything needed to reproduce
/// the invalidation on this or another host.
/// </summary>
public sealed class InvalidationCall
{
    public RpcMethodRef MethodRef { get; }   // service + method identity
    public ArgumentList Arguments { get; }   // call arguments, CancellationToken slot excluded
}
```

- `MethodRef` is the existing `RpcMethodRef` (a UTF-8 `service.method` name) where
  the method is RPC-exposed. Non-RPC compute methods (`protected virtual` ones such
  as `LiveVideoBackend.ListRaw`) need an equivalent name minted from
  `(serviceType, methodName, signature hash)` and registered in the same table.
- `Arguments` drops the `CancellationToken` slot (`MethodDef.CancellationTokenIndex`),
  so the recorded arity is `ParameterTypes.Length - 1` for the usual compute method.
- Equality/hash follow `ComputeMethodInput`'s, so a plan can dedupe on insert.

### Capture: `ComputeContext` + a new `CallOptions` flag

**Hot-path budget: a normal compute method call must read nothing new.** No extra
`AsyncLocal`, no extra branch outside the existing cold path. That constraint drives
the whole mechanism below.

The only thing `ComputeContext` carries is one more `CallOptions` bit:

```csharp
public enum CallOptions
{
    GetExisting = 1,
    Invalidate = 2 + GetExisting,
    Capture = 4,
    InboundRpc = 8,
    DeferInvalidate = 16 + GetExisting,
}
```

No new field on `ComputeContext`, so nothing grows the object that
`Computed.BeginCompute` allocates per computation. The recording hook goes where
invalidation already happens — `ComputeMethodFunction.ComputeServiceInterceptorHandler`,
the only place holding both `MethodDef` and `Invocation`:

```csharp
if ((context.CallOptions & CallOptions.DeferInvalidate) == CallOptions.DeferInvalidate) {
    DeferInvalidationScope.Current.Require().Add(MethodDef, invocation.Arguments);
    return MethodDef.DefaultResult;
}
if ((context.CallOptions & CallOptions.Invalidate) == CallOptions.Invalidate) {
    _ = ComputedImpl.TryUseExisting(computed, context);
    return MethodDef.DefaultResult;
}
```

**The defer target is resolved per recorded call, not once per block.** Caching it at
`Defer()` entry is wrong: a service's mutation routinely triggers invalidations that
belong to a *different* scope — a nested `Commander.Call` can open its own outermost
context, its own operation, and its own capture. Reading `DeferInvalidationScope.Current`
at record time puts the call in whichever capture is actually current; a target captured
at the outer block's entry would send those recordings to the outer operation, with the
wrong commit boundary and mode, and would ship calls belonging to an inner
operation that rolled back.

The cost is one `AsyncLocal` read on a path that only executes when `DeferInvalidate`
is set — i.e. inside a `Defer()` block, never on a normal compute method call. Since
capture blocks are brief by convention, the number of such reads per mutation is the
number of computeds it invalidates. The hot-path budget is intact.

**Remote compute services inside a delegate run locally, and that needs no work.**
`RemoteComputeMethodFunction` has no `CallOptions.Invalidate` special case — the call
lands in `ComputedImpl.TryUseExistingWithCallOptions`, which invalidates the locally
cached remote computed and returns `true`, so no RPC is ever issued. A `Local`
delegate runs under `Invalidation.Begin()` and inherits exactly that behavior. The only
addition is on the harvest path: a call resolving to a pure RPC client should invalidate
locally as it does today but **not** be recorded, since the host that owns the service
replicates its own invalidations — the same reasoning behind
`InvalidatingCommandCompletionHandler.IsRequired`'s `RpcServiceMode.Client` skip.

Two more consequences worth spelling out:

- Recording must happen **regardless of whether a local computed exists** — the origin
  host may have nothing cached while another host does. That is why the hook cannot
  live in `ComputedImpl.TryUseExistingWithCallOptions`: it is only reached with the
  existing computed in hand and returns early when it is `null`.
- **Don't put the capture on an ambient `ComputeContext`.** Having the commit boundary
  activate a carrier context with `CallOptions == 0` and reading the capture off
  `ComputeContext.Current` is technically workable inside command handlers — none of
  them wrap themselves in `Computed.BeginIsolation()`, so the
  `new ComputeContextScope(ComputeContext.None)` erasure that affects
  `LiveSessionsBackend.SetParticipation` and friends doesn't reach a capture block.
  It is still the wrong choice: it grows the `ComputeContext` that
  `Computed.BeginCompute` allocates per computation, and it re-introduces the
  entry-time caching this section just rejected. `DeferInvalidationScope`'s own
  `AsyncLocal` costs nothing on the hot path and carries the right lifetime.

### Service-type resolution registry

`MethodDef.Type` is the type the interceptor was built for — the *implementation*
type (`LiveVideoBackend`), or a proxy over it. To resolve the service on the
applying host we need the *registered service type* (`ILiveVideoBackend`).
`RpcServiceRegistry` is keyed by service type and client type, and skips
`RpcServiceMode.Local` services entirely, so it cannot answer this.

Add a small registry populated at registration time, where both types are already
known (`FusionBuilder.AddComputeService`, `FusionRpcServiceBuilder`'s
`CreateComputeService` / `CreateDistributedService`):

```csharp
public sealed class ComputeServiceRegistry
{
    public void Register(Type serviceType, Type implementationType);
    public Type? TryGetServiceType(Type implementationOrProxyType);
    public InvalidationCallTarget? TryGetTarget(RpcMethodRef methodRef);
}
```

Registering at *resolution* time (rather than registration time) also works and
covers proxies minted lazily, but registration time is deterministic and lets the
startup validation below run before the first request.

`InvalidationCallTarget` resolves `RpcMethodRef` → `(serviceType, ComputeMethodDef)`
so the applying host can invoke the method.

### Applying a call

Apply by **invoking the method through the service proxy** under
`Invalidation.Begin()`, not by poking `ComputedRegistry` directly:

```csharp
using (new RpcOutboundCallSetup(RpcHub.LocalPeer).Activate())
using (Invalidation.Begin(source))
    await methodDef.TargetAsyncInvoker.Invoke(service, arguments).ConfigureAwait(false);
```

This reuses the whole existing invalidation path — consolidation twins
(`ConsolidationSourceMethodDef` / `ConsolidationTargetMethodDef`),
`IHasInvalidationTarget` indirection, invalidation delays — instead of
re-implementing it. `RpcOutboundCallSetup(RpcHub.LocalPeer)` forces local execution,
the same trick `InvalidatingCommandCompletionHandler.TryInvalidate` already uses for
`RpcServiceMode.Distributed` services.

### Operation integration: where the calls migrate

Only `Replicated` puts anything on the operation. `Local` never does — its delegates run
in-process and nothing needs to be stored.

```csharp
public class Operation
{
    public InvalidationCallSet InvalidationCalls { get; }   // deduped, ordered; Broadcast only
}
```

**Harvest at `Commit()`, not after it.** Under `Replicated`, the delegates must be run in
recording mode inside `IOperationScope.Commit()`, as its first step:

```csharp
public async Task Commit(CancellationToken cancellationToken = default)
{
    if (Transport == InvalidationMode.Replicated)
        Operation.InvalidationCalls = await DeferInvalidationScope.Current
            .Harvest(cancellationToken).ConfigureAwait(false);
    ...
}
```

The timing is not negotiable. `DbOperationScope.Commit` builds
`new DbOperation(Operation)` and adds it to the `DbContext` *inside the transaction* —
so anything harvested after that point never reaches the row. Putting it in the
provider's `OnCommand` instead would get this wrong: the provider that owns the scope
(`InMemoryOperationScopeProvider`) runs **outside** the one that commits the DB scope
(`DbOperationScopeProvider`), so its post-chain code executes after the DB commit has
already been serialized. Harvesting in `Commit()` makes it automatically correct for
every scope type, present and future, and gives one rule to remember: **an operation's
invalidation calls are frozen at commit time.**

Then:

- **After commit, `Local`:** once the commit is verified, run the delegates under
  `Invalidation.Begin()`. Nothing was recorded, nothing is shipped.
- **After commit, `Replicated`:** apply the harvested calls locally; the operation record
  carries them, and other hosts apply them when the log reader delivers it.
- **On rollback:** `Local` never runs the delegates; `Replicated` harvested but never
  persisted, and never applies. Nothing to undo either way.
- **On reprocessing:** `OperationReprocessor` (priority 100,000) sits above both
  providers and re-runs the chain, so a fresh `DeferInvalidationScope` is opened per
  attempt. `Operation.InvalidationCalls` must be reset alongside it — the
  reprocessor preserves the operation `Uuid`, so the operation object must not carry
  the failed attempt's calls into the retry.

Transports:

| Mode | Carrier | Applies on |
|---|---|---|
| `Local` | none | the origin host only |
| `Replicated` | the operation log row (DB services) or `IOperationBroadcaster` (Redis/NATS) | every host |

For DB-backed services the calls ride along in the operation record. Shard-owned
services have no operation log, so `Replicated` for them needs a fan-out channel.
That channel must be reliable enough — an at-most-once pub/sub drop means a
permanently stale cached value, whereas the op log is ordered and gap-detecting.
`Replicated` over an unreliable transport is only acceptable for services that already
have a staleness backstop (the live services do: `computed.Invalidate(SelfHealDelay)`
plus Redis TTLs). Enforce that pairing at startup rather than leaving it to review.

## Choosing the mode per command

One enum, four values. An earlier draft split this into two axes — "how invalidation is
expressed" and "who applies it" — but the cross product contains combinations that
cannot occur, so a single enum is both smaller and safer:

| `InvalidationMode` | Handler body | What happens |
|---|---|---|
| `None` | neither a guard nor `Defer()` | Nothing. Treated exactly like an `IDelegatingCommand`: `IsRequired` already returns `false` for those, so this is a value the replay path understands today. |
| `Legacy` | `if (Invalidation.IsActive) { … return; }` | Today's behavior, untouched — including how `MustStore` decides whether the operation is logged. |
| `Local` | `Defer(...)` blocks | Delegates run in-process after commit. No recording, no log row needed for invalidation. |
| `Replicated` | `Defer(...)` blocks | Delegates are harvested at commit into `InvalidationCall`s and replicated cluster-wide via OF. |

`None` deserves the note: it is not "we don't know", it is "this command invalidates
nothing, and the nested commands it issues carry their own invalidation".
`ContactsBackend.OnSetIsBlocked` is exactly that today, with a comment saying so.

### Config may only move a service within the deferred pair

The mode is declared in code because it describes how the handler body is *written*.
But `Local` and `Replicated` share a body shape — both use `Defer(...)` blocks — so
moving between those two is safe from configuration, and that is where the genuine
deployment choice lives. Moving to or from `Legacy`/`None` is not, because the body
would have to change. So: **config may override `Local` ⇄ `Replicated` and nothing
else**, and an attempt to override anything else is a startup error.

### Why this must be enforced, not inferred

A `Legacy` handler carries an `if (Invalidation.IsActive) { …; return; }` guard; the
completion pass re-invokes it and the guard makes the second invocation harmless. A
`Local` or `Replicated` handler has **no guard**. If the replay fires on it, the
second invocation **re-runs the mutation** — a duplicate write, on every host. That is
not a degraded cache, it is data corruption.

The reverse mistake is cheap: if a `Legacy` handler is not replayed, some computeds
stay stale until their next TTL/self-heal or a restart. Visible, recoverable, boring.

**That asymmetry is the whole design rule: on any ambiguity, mismatch, or missing
information, do not replay.**

Inference cannot deliver this. "Did the handler open a `Defer()` block?" fails exactly
where you noted — a handler that legitimately invalidates nothing looks identical to a
legacy handler, and guessing wrong in the unsafe direction re-runs a mutation. So the
mode must be **declared**, and `None` exists precisely so "this command invalidates
nothing" is a statement rather than an absence of evidence.

### Where the declaration lives

Resolution order, most specific wins:

1. `[InvalidationMode(...)]` on the `[CommandHandler]` method — the migration override.
2. `[InvalidationMode(...)]` on the service implementation type — the normal unit,
   since a backend is converted as a whole.
3. The app-wide default, set once at OF registration, which stays `Legacy`.

```csharp
// 3. App-wide default: nothing changes until something opts in.
fusion.AddOperations(operations => operations
    .WithDefaultInvalidationMode(InvalidationMode.Legacy));
```

```csharp
// 2. Whole service converted - the common case.
[InvalidationMode(InvalidationMode.Local)]
public partial class LiveVideoBackend : ShardComputeService, ILiveVideoBackend
{
    // [CommandHandler]
    public virtual async Task OnRegister(
        LiveVideoBackend_Register command, CancellationToken cancellationToken)
    {
        var (chatId, streamInfo) = command;
        ... mutate Redis ...
        Invalidation.Defer(() => {
            _ = ListRaw(chatId, default);
            _ = List(chatId, default);
        });
    }
}
```

```csharp
// 1. Per-handler overrides - a service mid-conversion.
public partial class ContactsBackend : DbServiceBase<ContactsDbContext>, IContactsBackend
{
    // [CommandHandler]
    [InvalidationMode(InvalidationMode.Local)]
    public virtual async Task<Contact?> OnChange(
        ContactsBackend_Change command, CancellationToken cancellationToken)
    {
        ... mutate DB ...
        Invalidation.Defer(() => _ = Get(ownerId, id, default));
        return contact;
    }

    // [CommandHandler]
    // No attribute -> inherits Replay from the default; the guard stays.
    public virtual async Task OnTouch(
        ContactsBackend_Touch command, CancellationToken cancellationToken)
    {
        if (Invalidation.IsActive) {
            ...
            return;
        }
        ...
    }

    // [CommandHandler]
    // Invalidates nothing itself - the nested ContactsBackend_Change commands do it.
    [InvalidationMode(InvalidationMode.None)]
    public virtual async Task OnSetIsBlocked(
        ContactsBackend_SetIsBlocked command, CancellationToken cancellationToken)
    { ... }
}
```

That last one is not hypothetical: `OnSetIsBlocked`'s body today opens with
`if (Invalidation.IsActive) return; // The child ContactsBackend_Change commands handle invalidation`.
`None` says exactly that, and lets the analyzer enforce that the handler contains
neither a guard nor a `Defer()` block.

Only the `Local` ⇄ `Replicated` choice may additionally come from configuration:

```csharp
rpcHost.AddBackend<ILiveVideoBackend, LiveVideoBackend>(InvalidationMode.Local);
rpcHost.AddBackend<IContactsBackend, ContactsBackend>(InvalidationMode.Replicated);
```

```json
{ "Fusion": { "Invalidation": {
    "ILiveVideoBackend": "Local",
    "ILiveSessionsBackend": "Local",
    "IContactsBackend": "Broadcast"
} } }
```

The resolver is the boring part:

```csharp
public InvalidationMode Resolve(IMethodCommandHandler handler)
    => handler.Method.GetCustomAttribute<InvalidationModeAttribute>()?.Style
        ?? handler.GetHandlerServiceType().GetCustomAttribute<InvalidationModeAttribute>()?.Style
        ?? DefaultStyle;
```

### Why not a marker on the command

The alternative was a marker interface on the command record:

```csharp
// Rejected
public sealed partial record ContactsBackend_Change(...)
    : ICommand<Contact?>, IBackendCommand, IDeferredInvalidation;
```

It reads well, but it declares the wrong thing. The style is a property of the
*handler implementation* — does its body have the guard or the `Defer()` block — not of
the command contract. Concretely:

- Commands live in `*.Contracts` assemblies that would have to take a dependency on a
  Fusion invalidation marker for what is an implementation detail of the other side.
- The same command can be handled by different implementations, one converted and one
  not; the marker would be wrong for one of them, in the unsafe direction.
- Converting a service means touching every one of its command records rather than one
  attribute on the class.

And it buys nothing on the lookup side: `InvalidatingCommandCompletionHandler.IsRequired`
already resolves command → final handler → service type, so command-to-style is free
either way.

### Where it is applied

`IsRequired` already decides, per command, whether to replay — it returns `false` for
`IDelegatingCommand`, for pure RPC clients, for non-2-parameter handlers, for
non-`IComputeService` handlers. Style is one more disqualifier in the same place:

```csharp
if (styleResolver.Resolve(finalHandler) is not InvalidationMode.Legacy)
    return false;
```

Because the decision is per command and not per operation, **styles mix freely inside
one operation**: an outermost `Legacy` command with a nested deferred-mode one replays the
former and skips the latter, and `operation.NestedOperations` is walked the same way it
is today. No uniformity constraint, no migration lockstep.

### Enforcement

The two styles have mutually exclusive syntactic signatures, so this is checkable:

- **Analyzer.** A deferred-mode handler referencing `Invalidation.IsActive` → error.
  `Legacy` handler calling `Defer()` → error. `None` handler doing
  either → error. This is where most mistakes die, at compile time, next to the code.
- **Startup validation.** Resolve the style of every `[CommandHandler]` on every
  compute service and dump the table at startup, the way `RpcServiceRegistry` already
  dumps registered services. A mode that is auditable at a glance is a mode people can
  reason about; a silently-defaulted one is not.
- **Runtime tripwires.** `Defer()` or `BeginDeferred()` while `Invalidation.IsActive` →
  throw: an invalidation pass is running, so deferring inside it is either recursion or
  a deferred-mode handler being replayed. Note the check is specifically
  `Invalidation.IsActive`, **not** "are we inside a compute method" — a compute method
  can legitimately `Task.Run` into code that mutates and defers, and `ComputeContext`
  flows into that `Task.Run` through `ExecutionContext`. Blanket-rejecting a compute
  ancestry would break that; rejecting an active invalidation pass would not.
  A `Legacy` handler that recorded deferred calls → throw at commit.

### Enforced by construction: no replayable command

The three layers above are backstops. The actual guarantee is structural: **a
`Replicated` operation stores no replayable command at all** — its record carries the
list of invalidations to run instead. The replay then *cannot* fire, whatever any host
believes about the mode.

Backward compatibility is free: `CompletionProducer.OnOperationCompleted` already opens
with `if (operation.Command is not { } command) return;`, so an old host reading such a
row does nothing at all. Stale caches, the safe direction — never a re-run mutation.
That closes the rolling-upgrade hole from the version-skew section too.

Two consequences to build for:

- **Delivery needs its own listener.** Since `CompletionProducer` bails out on a
  command-less operation, applying `InvalidationCalls` on the receiving hosts cannot
  ride the `ICompletion` path. It needs a second `IOperationCompletionListener` that
  applies `operation.InvalidationCalls` directly, independent of whether a command is
  present.
- **Audit.** Losing the command from the record also loses it for diagnostics and
  correlation. If that turns out to matter, the command can be stored later in a field
  the replay path does not read — additive, and it does not weaken the guarantee as
  long as the replay path keeps ignoring it.

### Startup validation for the deferred modes

- `Local` requires the service to be routed to a single owner per key
  (`RpcServiceMode.Distributed` with a shard-based router). A `Local` service
  reachable on any host serves stale data indefinitely.
- `Replicated` requires a broadcast channel, and every compute method of the service to be
  addressable and argument-serializable.
- `Replicated` requires singleton services — a scoped compute service cannot be
  resolved on the applying host. (`ComputeServiceWithCommandHandlersMustBeSingleton`
  already blocks the related case.)

With the default mode left at `Legacy` and no `[InvalidationMode]` anywhere, nothing
changes: no scope is opened, `Defer()` throws if anyone calls it, and every handler is
replayed exactly as today.

## Version skew and rolling upgrades

This is the central trade-off of the design, and it deserves to be stated plainly:

> Replay is skew-robust by construction — the payload is the command, an already
> versioned wire contract, and the invalidation logic is whatever the receiving
> host's own binary says. Recorded invalidation calls move the *compute-method
> surface* into the wire contract.

Where the risk actually lives:

- **`Local` mode has no skew risk at all.** The calls are applied in-process by the
  binary that recorded them. Since the shard-owned services are the primary target
  and they are `Local`, the concern does not apply to the main use case.
- **`Replicated` + recorded calls** is where skew matters.

Case by case, for a mixed old/new cluster:

| Change | Old host receives | Result |
|---|---|---|
| New compute method added | unknown method → drop | **benign** — the old host has no such computed |
| Compute method removed | new host receives the old host's call → drop | **benign** — same reason |
| Method renamed | unknown name → drop | **stale cache** on the host that still has the old computed |
| Signature changed | unknown/incompatible → drop | **stale cache**, same as rename |
| Argument type changed shape | deserialization failure | **stale cache**, and noisy |

So only the *same logical computed addressed differently by the two versions* is
dangerous. Two mitigations, in order of preference:

1. **Reuse the existing RPC legacy-name machinery.** `RpcMethodRef` resolution already
   goes through `RpcMethodResolver` with `VersionSet` and `LegacyNameAttribute` /
   `LegacyNames`, which exists precisely to let a renamed method stay reachable from
   older peers. If invalidation calls are identified by `RpcMethodRef`, renames are
   covered by annotating the method, with no intermediate release.
2. **An intermediate release that records both**, for changes the legacy-name path
   cannot express (signature changes, argument type changes). One release records
   both the old and the new call; the next drops the old. This is the same discipline
   any wire-contract change needs.

Operationally, make skew *visible* rather than inferred: count dropped/unknown
invalidation calls per host as a metric, and log the first occurrence per method
name. A rolling upgrade that breaks invalidation then shows up as a counter, not as
a support ticket about stale data.

A conservative fallback also exists: keep the legacy command replay as the
`Replicated` payload for DB-backed services (they already have it, it works, and it
is skew-robust), and use recorded calls only for `Local`. That gives the unified
authoring form everywhere while confining the new wire contract to in-process use.
Worth doing for the first release.

## Other issues to design for

1. **Recording when nothing is cached locally.** Must record unconditionally; see
   the hook placement note above. Easy to get wrong, and the failure is invisible
   (invalidation silently stops crossing hosts for cold keys).
2. **Fan-out size.** `ChatsBackend.OnUpsertEntry` stashes a `Dictionary<string, long>`
   in `Items` and loops over it during the replay; recording one call per entry could
   mean thousands of entries in one operation. Options: dedupe (mandatory anyway),
   cap with a loud log, or keep replay for those specific handlers.
3. **Retries.** `OperationReprocessor` re-runs the command; the recorded set must be
   reset per attempt.
4. **Recursion.** `Defer()` reached from inside an invalidation pass (legacy replay)
   must be a no-op or throw, never re-record.
5. **Client-mode services.** A call naming a service this host registers as a pure
   RPC client should be dropped, mirroring
   `InvalidatingCommandCompletionHandler.IsRequired`'s `RpcServiceMode.Client` check.
6. **Non-serializable arguments.** Private compute methods may take types never
   designed to cross the wire. Validate at startup for `Replicated` services, not at
   the first invalidation.
7. **Version-tolerant argument types.** Argument records must be
   `MemoryPackable(GenerateType.VersionTolerant)` (or equivalent) for `Replicated`,
   same as any RPC payload.
8. **Op-record forward compatibility.** Older hosts must tolerate the new field. A
   dedicated nullable column is safer than a reserved `Operation.Items` key, since an
   unknown value type inside the bag can break deserialization of the whole bag.
9. **Shard handover.** Under `Local`, invalidations recorded on the old owner never
   reach the new one — already handled, because every value depends on the shard
   ownership computed via `RequireShardOwnership(addDependency: true)`, so a handover
   invalidates the whole shard. Worth an explicit test rather than an assumption.
10. **Ordering.** Irrelevant for correctness, for the reason already documented in
    `InvalidatingCommandCompletionHandler.OnCommand`: the last invalidated dependency
    wins regardless of order.
11. **Promptness.** `CompletionProducer` dispatches completions via `Task.Run`, so
    replay-based invalidation is not ordered against the caller's `await`. `Local`
    mode should apply calls before the mutating call returns, matching what the shard
    services do today.

## Reuse

**Existing abstractions this builds on:**

| Need | Reuse |
|---|---|
| invalidation scope, source tracking | `Invalidation.Begin`, `ComputeContext`, `CallOptions`, `InvalidationSource` |
| the interception point holding `MethodDef` + `Invocation` | `ComputeMethodFunction.ComputeServiceInterceptorHandler` |
| existing invalidation semantics (consolidation, delays) | `ComputedImpl.TryUseExisting*`, `IHasInvalidationTarget` |
| canonical "which computed" identity | `ComputeMethodInput`, `ComputeMethodDef` |
| method identity, legacy names, version negotiation | `RpcMethodRef`, `RpcMethodResolver`, `LegacyNameAttribute`, `LegacyNames`, `VersionSet` |
| argument capture, invocation | `ArgumentList`, `MethodDef.ArgumentListType`, `MethodDef.TargetAsyncInvoker`, `MethodDef.CancellationTokenIndex` |
| argument serialization | `RpcArgumentSerializer` |
| carrier | `Operation`, `IOperationScope`, `Operation.AddCompletionHandler` |
| ephemeral (no-DB) operations | `InMemoryOperationScope`, `InMemoryOperationScopeProvider`, `Operation.MustStore(false)` |
| transport hooks | `IOperationCompletionListener`, `IOperationCompletionNotifier`, `CompletionProducer` |
| reliable broadcast for DB services | `DbOperationScope`, `DbOperationsBuilder`, the op-log reader |
| commit verification, retry dedupe | `DbOperationScope.VerifyCommit`, `DbOperation` / `DbEvent` verifier rows, `CommitVerificationPolicy` — kept as-is |
| legacy replay path (kept) | `InvalidatingCommandCompletionHandler` |
| forcing local execution while invalidating | `RpcOutboundCallSetup(RpcHub.LocalPeer)` |
| metrics/tracing | `FusionInstruments.InvalidationPassDuration`, `InvalidationPassCommandCount` |
| single-owner routing, handover safety (ActualChat) | `ShardOwner.RequireShardOwnership`, `MeshRpcRoute`, `ShardScheme` |

**New components and placement:**

| Component | Placement | Rationale |
|---|---|---|
| `InvalidationCall`, `InvalidationCallSet` | `ActualLab.Fusion` (`Operations`) | Fusion-wide concept, meaningless outside it |
| `DeferInvalidationScope` | `ActualLab.Fusion` | must be usable without CommandR/OF — that is the whole point |
| `Invalidation.BeginDeferred(handler?)`, `Defer(Action)` / `Defer(Func<Task>)` | `ActualLab.Fusion` | extends the existing `Invalidation` static |
| `IDeferInvalidationHandler` + default handler | `ActualLab.Fusion` | the extension point OF plugs into |
| `CallOptions.DeferInvalidate` | `ActualLab.Fusion` | one flag next to `Invalidate`; no new `ComputeContext` field |
| scope open/dispose + drain (the OF integration) | `InMemoryOperationScopeProvider`, `IOperationScope.Commit` | OF handlers *use* `DeferInvalidationScope`; the primitive stays free of Commander types |
| `ComputeServiceRegistry` (impl/proxy type → service type) | `ActualLab.Fusion.Interception` | next to `ComputeMethodDef`; `RpcServiceRegistry` cannot answer this |
| `InvalidationMode` (`None`/`Legacy`/`Local`/`Replicated`), `[InvalidationMode]`, resolver, analyzer | `ActualLab.Fusion` registration API | one axis, per handler; the unsafe-direction guard |
| `IOperationBroadcaster` | `ActualLab.Fusion` | abstraction, so it isn't ActualChat-only |
| Redis / NATS `IOperationBroadcaster` | ActualChat `Core.Server`, or `ActualLab.Fusion.Ext` | infrastructure-specific |

Nothing here is ActualChat-specific except the broadcaster implementations.

## Rollout

The delegate form splits the work cleanly: **phases 1–4 ship `Local` only and need none
of the recording machinery.** `InvalidationCall`, `ComputeServiceRegistry`, argument
serialization, and the whole version-skew surface arrive only with `Replicated`, in
phase 6.

1. `DeferInvalidationScope`, `Invalidation.Defer(...)`, `InvalidationMode` +
   `[InvalidationMode]` + resolver + the `IsRequired` disqualifier, `WithDefaultInvalidationMode`.
   All inert: the default is `Legacy`, nothing declares otherwise, no scope is opened.
2. Give shard-service mutators an explicit `using var _ = Invalidation.BeginDeferred();`
   at the top. The default handler invalidates at scope exit, so these need no
   operation, no command types, and no Commander pass on heartbeat-frequency calls
   (`Register`, `SetParticipation`).
3. Convert the live/streaming services to `[InvalidationMode(InvalidationMode.Local)]`,
   moving their inline `Invalidation.Begin()` calls into `Defer(...)` delegates. Check the read-back sites
   (`SetParticipation` → `IsSessionLive`, `EvaluateLiveness`) explicitly — those are the
   ones that break if something is deferred that must be immediate.
4. Split `MustStoreOperation` into `MustBroadcastOperation` derived from the mode,
   and drop the hand-written `MustStore(false)` calls
   (`UserPresencesBackend.OnCheckIn` is the one to convert first). Commit verification
   is untouched.
5. Migrate `if (Invalidation.IsActive)` blocks to `Defer(...)`, service by service,
   deleting the matching `Operation.Items` marshalling. ~80 files; mechanical but
   per-file review is required, because those `Items` flags encode conditions that
   become ordinary `if`s. Still `Legacy`-equivalent behavior via `Replicated`, so this
   phase depends on 6.
6. `InvalidationCall`, harvest mode, `ComputeServiceRegistry`, a broadcast transport,
   the skew mitigations and the drop-counter metric.

## Decisions

- **A deferred delegate that throws** → log + metric + leave the computed stale. The
  command has already committed, so failing it is not an option. The counter is not
  optional either: this is silent data staleness, and it needs to be visible.
- **`Defer()` / `BeginDeferred()` inside an active invalidation pass** → throw. The
  check is `Invalidation.IsActive` specifically, not "is there a compute ancestry" —
  see the tripwire note above.
- **A deferred-mode operation stores no replayable command**, only the list of
  invalidations to run. Enforcement by construction; a command may be added back later
  for audit, in a field the replay path ignores.
- **Deferred delegates run on `CancellationToken.None`**, host shutdown included.
- **`Defer(...)` may be called any number of times**, anywhere in the handler.
- **Remote compute calls inside a delegate** run locally already; only the harvest path
  needs the pure-RPC-client skip.

## Fan-out: pseudo-methods, not a framework feature

A handler that invalidates thousands of computeds would produce thousands of
`InvalidationCall` entries. `ChatsBackend.OnUpsertEntry` is the shape to worry about —
it stashes a whole `Dictionary<string, long>` in `Operation.Items` today and loops over
it during the replay.

The framework-level fix would be a coarse entry kind — "invalidate everything matching
this tag/prefix" — so one entry replaces thousands. That is **rejected**:
`ComputedRegistry` is a hash map keyed by full `ComputedInput` equality, so tag matching
would mean maintaining a secondary index on every computed registration, on Fusion's
hottest path, to serve a rare case.

The right answer is the one Fusion already documents: **pseudo-methods**
(see [`PartAC-PM.md`](../PartAC-PM.md) — "invalidation groups or colors"). A handler
anticipating unenumerable fan-out introduces a shared dependency, has the real methods
depend on it, and invalidates that one pseudo-method. The dependency graph does the
fan-out, so this collapses to a **single** `InvalidationCall` — which also means one
entry on the wire and one stable method identity instead of thousands of argument
tuples, so it shrinks the version-skew surface too. No `ComputedRegistry` change, no new
entry kind.

Where a pseudo-method genuinely doesn't fit, the fallback is external storage: park the
ID list in Redis or a side table keyed by the operation `Uuid`, and put a single
reference in the operation record for receiving hosts to fetch.

**No cap, but a loud log.** Deferred calls are not truncated — silently dropping
invalidations is exactly the failure mode this design exists to avoid. Instead, warn
above a configurable threshold, and **only under `Replicated`**, where the calls have to
be persisted into the operation record and their count is what actually costs something.
Under `Local` nothing is persisted and the count is irrelevant.

## Open questions

- Whether `Replicated` ships recorded calls at all in v1, or keeps command replay as
  the cross-host payload (the conservative option recommended above).
- Whether the `DbEvent`-as-commit-verifier reuse is worth cleaning up eventually. It
  works and the alternatives cost more today, but it does mean the events table's
  trimming policy governs commit verification.
