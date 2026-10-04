# Migrating to 15.0

15.0 replaces the Operations Framework's replay-based invalidation with
[deferred invalidation](./PartO-IM.md). Two things follow from that and need action now, and one
changes how you deploy from here on &ndash; see [Why this changed](#why-this-changed) for the last
one:

- **Every command handler that mutates state has to change.** It declares its invalidations with
  `Invalidation.Defer(...)` and a `[DeferredInvalidationMode]`. There is no default mode: a handler
  that defers without declaring one throws.
- **The `_Operations` and `_Events` tables change shape.** `_Events` has to be **drained before the
  upgrade** &ndash; a pending event is real work, and it should be done by the deployment that wrote
  it. `_Operations` needs nothing: its old rows become inert. The hosts can't be upgraded one at a
  time, though.

If you don't use the Operations Framework &ndash; no `AddOperations()`, no `[CommandHandler]` that
mutates &ndash; none of this applies, and 15.0 is a drop-in upgrade apart from the
[renames](#renames-and-moved-types).

## Why this changed

The goal was to simplify, and replay was the ugly part of the old design. To learn what a handler had
invalidated, the framework ran the handler's body a second time with `Invalidation.IsActive` true and
expected it to notice and touch only the compute methods it wanted invalidated. One body served two
purposes, every mutating handler opened with a branch that had nothing to do with its job, and the
framework was in the business of re-executing arbitrary application code whose side effects it
couldn't know.

### What replay was buying

Replay wasn't ugly by accident. It bought **tolerance across versions during a rolling deployment**,
and that's worth being explicit about, because the new model gives some of it up.

Under replay, the only thing that crossed hosts was **the command**. Each host then re-ran *its own*
copy of the handler to decide what to invalidate. So an old host replaying a command a new host wrote
&ndash; or the reverse &ndash; mostly worked: if the command still deserialized, the rest followed,
using whatever that version considered the right invalidations.

The new model sends **the invalidation calls themselves**: a service type, a method, and its
arguments. That is a reference to something the receiving host has to still have, with the same
shape. While two versions coexist, invalidation only works if the methods being invalidated still
exist on both and their signatures still match &ndash; otherwise a recorded call is dropped on the
host that can't resolve it, and dependent caches there stay stale until something else invalidates
them. [Deployment Compatibility Contract](./PartO-Serialization.md#deployment-compatibility-contract)
spells out the rules and the failure mode.

**So the trade is explicit: a simpler invalidation block, paid for with upgrades that need more
care.** It is not that the new model is less clever &ndash; it is just different, and easier to
understand. What got harder is version-to-version evolution of server-side code, and that is the
honest cost of the change.

One more thing it costs: **invalidation calls are serialized now, so their number matters.** Under
replay you could invalidate an arbitrarily large set in a single command at no storage cost, because
nothing was recorded. Keep it to a reasonable number instead. In practice the need for a huge set has
never come up &ndash; a command that invalidates hundreds of distinct keys is usually a sign the
compute methods are keyed too finely.

### What it buys

**The same invalidation block works everywhere.** This is the main advantage. The block is a closure
naming compute methods, so it doesn't depend on the mode at all &ndash; `Local`, `Replicated` and
`Distributed` change only how far the result travels, never what you write. One of Fusion's core
premises is that you write code once and then move it, gradually, to a more scalable execution model;
invalidation now follows that premise instead of working against it. You don't need a different kind
of invalidation block per mode inside your commands.

**And it's geared toward `Distributed`, which is now the primary mode.** `Replicated` is a fine
default and it's simple &ndash; every host applies every invalidation as it reads the log &ndash; but
it scales badly: the work is duplicated on every host, so the cost of one invalidation grows with the
size of the cluster. `Distributed` routes each call to the host that owns the value, so the cost per
invalidation stays flat as hosts are added, which is what makes effectively unbounded horizontal
scaling possible. Replay fit `Replicated` naturally and `Distributed` badly; the new model is the
other way round.

## Step 1: Drain what you can from `_Events`

**A 14.x event stays readable under 15.0**, so this step is about *when its work runs*, not about
whether its payload survives. 15.0 reads whichever column holds a payload, and its text path is the
same `NewtonsoftJsonSerializer` on the same `typeof(object)` that 14.x wrote with, so a row with only
`ValueJson` deserializes unchanged. What draining buys you is that a pending event &ndash; one with
`State = 0` (`New`) &ndash; is work that hasn't happened, and if its value is a *command*, running it
under 15.0 hands it to a handler whose invalidation contract has changed underneath it. Where you can
let the deployment that wrote those rows finish them, do.

**You often can't finish them all.** A delayed event &ndash; `DelayUntil` in the future &ndash; is
`State = New` and no amount of waiting clears it before it's due, which may be months out. That's
normal, and it's why Step 3 keeps both column families: those rows have to survive the upgrade and
be read by 15.0 later.

1. **Stop accepting commands** on every 14.x host, but leave the hosts running so the event
   processor drains what's already due.
2. **See what's left, and why:**

   ```sql
   -- Pending overall, and the part that isn't due yet
   select count(*) filter (where "DelayUntil" <= now()) as due_now,
          count(*) filter (where "DelayUntil" > now())  as not_yet_due
   from "_Events" where "State" = 0;
   ```

   `due_now` should reach 0; if it stalls above 0, something is failing to process rather than
   lagging, so check the event processor's logs before going further &ndash; an event that has
   exhausted its retries ends up `Discarded` (`State = 2`), not `New`. A non-zero `not_yet_due` is
   expected and not a blocker: those rows carry over and 15.0 will read them when they come due.
3. **Stop every host**, decide the format (Step 2), apply the schema migration (Step 3), then deploy
   15.0.

### `_Operations` needs no draining

An operation row exists so that *other* hosts can invalidate their caches from it. The upgrade
restarts every host, so every cache starts empty &ndash; there is nothing stale for those rows to
invalidate, and nothing is lost by never reading them.

They're inert in any case: 14.x kept what an operation invalidated in `ItemsJson`, and 15.0 reads
`InvalidationCallsJson` / `InvalidationCallsData`. The names don't collide, so a leftover 14.x row
deserializes to an operation with no invalidation calls, applies nothing, and is removed by the
trimmer on age like any other. You can `truncate` the table during the migration if you'd rather not
look at them, but leaving them is equally fine.

### Still don't run 14.x and 15.0 at once

A rolling upgrade across this particular boundary doesn't work, and it's the one case the
cold-cache argument above doesn't cover: the two versions have no invalidation representation in
common. 14.x writes `ItemsJson` and expects peers to replay the command; 15.0 writes recorded calls
and expects peers to apply them. Neither can read the other, so for as long as they overlap, a host
that's been up long enough to have a warm cache gets silent staleness from every operation the other
version writes. Stop everything, migrate, then start &ndash; the restart is what makes the
cold-cache argument hold.

Rolling deployments *between* 15.x versions are fine and normal, but they're no longer free: see
[What replay was buying](#what-replay-was-buying).

## Step 2: Decide the serialization format first

It determines what your schema needs, so settle it before writing the migration.

14.x had one column per payload and always wrote JSON. 15.0 declares two per payload &ndash; one text,
one binary &ndash; and writes **exactly one** of them, whichever
`DbLogEntrySerializer.Format` names. It **defaults to `DataFormat.Bytes`** (MessagePack), so an
upgrade that touches nothing moves new rows to the binary columns and they stop being
human-readable.

That's usually what you want &ndash; smaller and faster &ndash; but it is a silent change, so choose
deliberately. To stay on text as 14.x did:

```cs
services.AddSingleton(_ => DbLogEntrySerializer.Default with {
    Format = DataFormat.Text,
});
```

Whichever you pick, your existing rows are all in the `*Json` columns and stay readable: a read
takes whichever column holds the payload. The one call that could strand them,
[`IgnoreUnusedOperationsFrameworkColumns`](#dropping-the-unused-columns), keeps `DbEvent.ValueJson`
mapped by default for exactly that reason.

The format only decides what *new* rows look like; a read takes whichever column actually holds the
payload. That's what lets 14.x rows, 15.0 text rows and 15.0 binary rows sit in one table and all
still deserialize.

## Step 3: The schema migration

**Declare both column families** for `_Events`, whichever format you chose &ndash; 15.0 reads
whichever column holds a payload, and a 14.x row written to `ValueJson` has to stay readable because
[you can't drain every event](#step-1-drain-what-you-can-from-events). If you call
[`IgnoreUnusedOperationsFrameworkColumns`](#dropping-the-unused-columns) this is already what you
get: it keeps `ValueJson` mapped by default.

Both formats:

| Table | Change |
|---|---|
| `_Operations` | **drop** `ItemsJson` and `NestedOperations` |

| Table | Change |
|---|---|
| `_Operations` | **add** `CommandData` (blob, null), `InvalidationCallsJson` (text, null), `InvalidationCallsData` (blob, null) |
| `_Operations` | `CommandJson` becomes **nullable** |
| `_Events` | **add** `ValueData` (blob, null) |
| `_Events` | `ValueJson` becomes **nullable** |

The `*Json` columns become nullable because a payload lives in exactly one of its two columns, and a
row that carries it in the other one leaves this side empty.

With EF migrations this is the usual `dotnet ef migrations add` against your `DbContext`. Read the
generated migration before applying it: it should contain exactly the rows above, and if it mentions
your own entities, that's a change you made rather than one 15.0 asked for.

**This step is easy to skip and the build won't tell you.** The entity changes come from the package,
so everything compiles; the failure arrives at runtime, as
`column "CommandData" of relation "_Operations" does not exist` on the first command, or as EF's
`PendingModelChangesWarning` if your tests check for it.

### Dropping the unused columns

`IgnoreUnusedOperationsFrameworkColumns` maps away the columns your format never writes, so each
payload keeps one column instead of two:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // Taking the serializer rather than a bare DataFormat: it can't disagree with the one
    // the app actually registered
    modelBuilder.IgnoreUnusedOperationsFrameworkColumns(DbLogEntrySerializer.Default);
}
```

**`DbEvent.ValueJson` is kept even under `Bytes`**, because `DbLogEntrySerializer`'s
`MustDeserializeLegacyEvents` defaults to `true`. That's what makes this call safe during an
upgrade: a delayed event can sit at `State = New` with `DelayUntil` months out, and dropping the
column its payload lives in would strand it. A read prefers `ValueData` and falls back to
`ValueJson`, so the only cost is one always-`NULL` column.

Set it to `false` once you're certain no event predates your current format &ndash; and only then:

```cs
services.AddSingleton(_ => DbLogEntrySerializer.Default with {
    MustDeserializeLegacyEvents = false,
});
```

`_Operations` needs no such latch. Its rows are history once the readers have caught up, and the
trimmer removes them on age (`MaxEntryAge`, 30 minutes by default), so its `*Json` columns are
dropped either way.

Under `DataFormat.Text` the flag does nothing: `ValueJson` is the column being written, and mapped
regardless. Mind the direction of the format argument in general &ndash; passing `DataFormat.Text`
because you "always used JSON", while the serializer still has its default `Bytes`, unmaps the
column your writer is using. The overload above avoids that by construction.

[Operations Framework Serialization](./PartO-Serialization.md) covers the format switch, the
serializers behind each column, and the deployment compatibility contract in full.

## Step 4: Rewrite the handlers

The mutation half of a handler doesn't change. The invalidation half stops being a second pass over
the same body and becomes a delegate that runs once the mutation is durable.

Before &ndash; the handler ran twice, and the second pass was the invalidation:

```cs
[CommandHandler]
public virtual async Task SetName(Cart_SetName command, CancellationToken cancellationToken)
{
    if (Invalidation.IsActive) {
        _ = GetCart(command.CartId, default);
        _ = ListCarts(default);
        return;
    }

    var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
    await using var _ = dbContext.ConfigureAwait(false);
    var dbCart = await dbContext.Carts.FindAsync([command.CartId], cancellationToken);
    dbCart!.Name = command.Name;
    await dbContext.SaveChangesAsync(cancellationToken);
}
```

After &ndash; it runs once, and names its invalidations where it already knows them:

```cs
[CommandHandler]
public virtual async Task SetName(Cart_SetName command, CancellationToken cancellationToken)
{
    var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
    await using var _ = dbContext.ConfigureAwait(false);
    var dbCart = await dbContext.Carts.FindAsync([command.CartId], cancellationToken);
    dbCart!.Name = command.Name;
    await dbContext.SaveChangesAsync(cancellationToken);

    Invalidation.Defer(() => {
        _ = GetCart(command.CartId, default);
        _ = ListCarts(default);
    });
}
```

Note what the handler *doesn't* carry: the mode. **Declare it on the service, not on each handler.**
How far an invalidation has to reach follows from how the service's data is stored and shared, which
is a property of the service rather than of one method &ndash; so in practice every handler on a
service wants the same mode. Declaring it once also keeps a handler you add later from throwing
because somebody forgot the attribute:

```cs
// Covers every [CommandHandler] on the service
[DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
public class CartService(IServiceProvider services)
    : DbServiceBase<AppDbContext>(services), ICartService
{
    // ... handlers, no attribute of their own
}
```

Put it on the **interface** instead when clients share that contract. The resolution order is the
method, then the implementation the container maps the service type to, then the method's declaring
type, then the interface &ndash; first one found wins &ndash; so a method-level attribute stays
available for the odd handler that genuinely differs from its service. See
[Declaring the Mode](./PartO-IM.md#declaring-the-mode).

Three quirks that bite during this rewrite:

- **A block sees the *final* value of a captured local**, not its value at the `Defer(...)` call
  site. Ordinary closure semantics, but the call reads as if it executes in place. Snapshot into a
  fresh local if you need the earlier value.
- **The mode comes from the first handler that defers**, not from the outermost command. A handler
  that defers nothing has no say, which is what lets a command delegate to handlers whose mode it
  doesn't know. Two handlers that need *different* modes throw rather than one being silently
  narrowed &ndash; one operation gets one carrier.
- **A command can no longer be called from inside an invalidation block.** `InvalidationGuard`
  rejects it. Nothing replays a handler now, so such a command would silently skip its operation
  scope.

**Delete every `Invalidation.IsActive` check you have**, not just the ones at the top of a handler.
A command cannot run during an invalidation or capture pass at all &ndash; `InvalidationGuard`
rejects it before any other filter sees it &ndash; so any such check inside command-scoped code is
unreachable. The framework removed its own: the operation scope providers and the reprocessor used
to each opt out on `Invalidation.IsActive`, and the guard now makes that one decision in one place
rather than three. `Invalidation.IsActive` remains meaningful only in code a *block* can reach, such
as a compute method deciding whether it is being invalidated.

Which mode to declare is the one real decision here;
[Choosing a Mode](./PartO-IM.md#choosing-a-mode) is the short version, and
`Local` is the one that always works if you're unsure. Note that `Replicated` and `Distributed` both
require a **stored** operation &ndash; they throw on a transient one, because the row is what carries
the calls to the other hosts.

## Testing invalidation after the rewrite

Tests written against replay often asserted synchronously right after the command returned, because
the replay pass ran inline. Deferred invalidation keeps that property for `Local`, but not for the
other two: `Replicated` applies the calls on the origin before the command returns and routes nothing,
while `Distributed` routes each call to its owner in the background, so a test that reads another
host's cache is racing it.

`Computed.Capture` is still how you get a handle on a computed to assert against:

```cs
var computed = await Computed.Capture(() => service.GetCart(cartId));
computed.IsConsistent().Should().BeTrue();

await commander.Call(new Cart_SetName(cartId, "new name"));

// Local and Replicated invalidate the origin's own copies before the call returns
computed.IsConsistent().Should().BeFalse();
```

For anything that crosses a host &ndash; a `Distributed` invalidation, or a peer applying a
`Replicated` one as it reads the log &ndash; use `ComputedTest.When` instead of a sleep. It
re-evaluates the assertion inside a `ComputedSource<T>`, so it retries when the values it reads are
invalidated and rethrows the last failure on timeout:

```cs
using ActualLab.Fusion.Testing;

await commander.Call(new Cart_SetName(cartId, "new name"));

await ComputedTest.When(async ct => {
    var cart = await otherHost.GetRequiredService<ICartService>().GetCart(cartId, ct);
    cart.Name.Should().Be("new name");
}, TimeSpan.FromSeconds(5));
```

Two things that make these tests honest rather than merely green:

- **Assert the mutation count**, not just the invalidation. Under replay a handler body ran twice, so
  a test that only checked the final value couldn't see a double mutation. A counter incremented in
  the handler and asserted as `1` is what pins that the body runs once now.
- **`Replicated` and `Distributed` need a stored operation**, so a test on a transient one throws
  rather than silently falling back. If a mode test passes suspiciously easily, check that the service
  is actually running against a database.

## Renames and moved types

| 14.x | 15.0 |
|---|---|
| `InMemoryOperationScope` | `TransientOperationScope` |
| `InMemoryOperationScopeProvider` | `TransientOperationScopeProvider` |
| `Operation.Items` | Removed. It existed to carry data into the replayed branch, and a deferred block closes over the handler's own locals instead |
| `Operation.NestedOperations`, the `NestedOperation` type | Removed &ndash; a nested handler's blocks join the one operation |
| `Operation.SuppressNestedOperationLogging()` | Removed, with the nested operations it suppressed |
| `Operation.ClearEvents()` | `Operation.RemoveEvents()`, which also takes a predicate overload |
| `RpcArgumentSerializer` | `ArgumentListSerializer`, in `ActualLab.Interception` |
| `ArrayBuffer.MustClean` | `ArrayBuffer.MustClear` |

`ServiceCall`, `Operation.InvalidationCalls` and `OperationCompletion` are new in 15.0 rather than
renames of anything &ndash; 14.x recorded nothing, which is what replay was for.

`ArgumentListSerializer` moving to `ActualLab.Interception` takes the type serializers, `NullValue`
and `RpcSerializableAttribute` with it. Argument serialization isn't RPC-specific &ndash; anything
that records a call and replays its arguments needs it, which is exactly what an invalidation call
is.

## Command handler priorities moved

Only relevant if you have a custom command filter whose priority sits between these:

| Handler | 14.x | 15.0 |
|---|---|---|
| `DbOperationScopeProvider` | 1,000 | **9,900** |
| `DeferredInvalidationScopeProvider` | &ndash; | **9,000** (new) |

The new filter opens the deferred invalidation scope, and it sits below every operation scope
provider so that a scope is always in place before anything can be deferred. A filter that used to
run below `DbOperationScopeProvider` at, say, priority 500 still does.

## Further Reading

- [Operations Framework: Invalidation Modes](./PartO-IM.md) &ndash; the three modes, declaring them,
  nested commands
- [Operations Framework Serialization](./PartO-Serialization.md) &ndash; formats, serializers, schema
  evolution
- [Operations Framework](./PartO.md) &ndash; the framework these belong to
- [Changelog](./CHANGELOG.md) &ndash; the full 15.0 entry
