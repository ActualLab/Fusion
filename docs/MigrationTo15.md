# Migrating to 15.0

15.0 replaces the Operations Framework's replay-based invalidation with
[deferred invalidation](./PartO-IM.md). Two things follow from that, and both need action:

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

The goal was to simplify. The original Operations Framework idea is a good one &ndash; a command's
invalidations belong with the command, and the log row is what carries them to the other hosts
&ndash; but *replay* was the ugly part of it. To find out what a handler had invalidated, the
framework ran the handler's body a second time with `Invalidation.IsActive` true, and the handler was
expected to notice that and touch only the compute methods it wanted invalidated.

That is tricky to understand and easy to get wrong. One body served two purposes, every mutating
handler opened with a branch that had nothing to do with its actual job, and the framework was in the
business of re-executing arbitrary application code whose side effects it couldn't know. A handler
that forgot the branch, or put a mutation above it, misbehaved in ways that were hard to see.

The replacement is deliberately less clever. A handler names its invalidations once, as a block, at
the point where it already knows them; the framework runs that block after the mutation is durable
and never re-enters the handler. It is arguably a bit less robust in places and it has quirks of its
own &ndash; the closure captures final values, the mode has to be declared, a block that fails leaves
values stale rather than failing the command &ndash; but it is far simpler to hold in your head, and
the quirks are the kind you can see.

The part that pays off most in daily use: **the invalidation block is the same block in all three
modes.** Because the block is just a closure naming compute methods, it doesn't depend on the mode at
all. `Local`, `Replicated` and `Distributed` change only how far the result travels, never what you
write. You can move a service from one mode to another &ndash; or declare a different mode per
handler in the same service &ndash; without touching a line inside any block. Under replay, how the
invalidation reached other hosts was tangled up in what the second pass did.

## Step 1: Drain `_Events`

Of the two tables, only `_Events` needs draining, and the reason is simply that a pending event is
**work that hasn't happened yet**. A row with `State = 0` (`New`) is an event the processor still
owes someone &ndash; and if its value is a command, processing it under 15.0 would hand a 14.x
payload to a handler whose invalidation contract has changed underneath it. Let the deployment that
wrote those rows finish them.

1. **Stop accepting commands** on every 14.x host, but leave the hosts running so the event
   processor keeps draining.
2. **Confirm `_Events` has nothing pending:**

   ```sql
   -- Must return 0 before you migrate
   select count(*) from "_Events" where "State" = 0;
   ```

   If it doesn't reach 0, something is failing to process rather than merely lagging &ndash; check
   the event processor's logs before going further. An event that has exhausted its retries ends up
   `Discarded` (`State = 2`), not `New`, so it won't hold you here.
3. **Stop every host**, apply the schema migration (Step 2), then deploy 15.0.

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

A rolling upgrade is the one case where the above doesn't save you: a 15.0 host that has been up
long enough to have a warm cache won't learn anything from an operation a 14.x host writes, and vice
versa. That's silent staleness for as long as the two versions overlap. Stop everything, migrate,
then start &ndash; the restart is what makes the cold-cache argument hold.

## Step 2: The schema migration

| Table | Change |
|---|---|
| `_Operations` | **drop** `ItemsJson` |
| `_Operations` | **drop** `NestedOperations` |
| `_Operations` | **add** `InvalidationCallsJson` (text, null) and `InvalidationCallsData` (blob, null) |
| `_Operations` | **add** `CommandData` (blob, null) |
| `_Operations` | `CommandJson` becomes **nullable** |
| `_Events` | **add** `ValueData` (blob, null) |
| `_Events` | `ValueJson` becomes **nullable** |

The `*Json` columns become nullable because a payload now lives in exactly one of its two columns,
and which one depends on the format &ndash; see [Step 3](#step-3-serialization-now-writes-binary-by-default).

With EF migrations this is the usual `dotnet ef migrations add` against your `DbContext`; the entity
changes come from the package, so the generated migration should contain exactly the rows above. Read
it before applying it: if it contains anything about your own entities, that's a change you made,
not one 15.0 asked for.

## Step 3: Serialization now writes binary by default

14.x had one column per payload and always wrote JSON. 15.0 has two, and
**`DbLogEntrySerializer.Format` defaults to `DataFormat.Bytes`** &ndash; MessagePack. Upgrade without
touching anything and your new rows stop being human-readable.

That's usually what you want: it's smaller and faster. But it is a silent change, so decide
deliberately. To keep text:

```cs
// Keeps _Operations and _Events on their *Json columns, as 14.x did
services.AddSingleton(_ => DbLogEntrySerializer.Default with {
    Format = DataFormat.Text,
});
```

Two consequences worth knowing:

**Reads take whichever column carries the payload**, not the one `Format` names. Your 14.x rows are
in `*Json` and stay readable regardless of which format you pick, which is why the format is not
itself a data migration.

**Don't drop the unused columns yet.** `IgnoreUnusedOperationsFrameworkColumns(format)` maps away the
columns a format never writes, and it's tempting right after a migration:

```cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // The format here MUST be the one the registered DbLogEntrySerializer writes with.
    // Mapping away the column the writer uses loses the payload silently.
    modelBuilder.IgnoreUnusedOperationsFrameworkColumns(DataFormat.Bytes);
}
```

Right after upgrading from 14.x the table holds **both** formats: old rows in `*Json`, new ones in
`*Data`. Mapping either side away makes the other side's rows unreadable. Wait until the trimmer has
removed every 14.x row, then drop the columns. And note the direction of the trap: if you have
"always used JSON" in mind and pass `DataFormat.Text` while the serializer still defaults to
`Bytes`, you have mapped away the column your writer is using.

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
[DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
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

## Renames and moved types

| 14.x | 15.0 |
|---|---|
| `InvocationRecord` | `ServiceCall`, in `ActualLab.CommandR.Operations` |
| `InMemoryOperationScope` | `TransientOperationScope` |
| `InMemoryOperationScopeProvider` | `TransientOperationScopeProvider` |
| `Operation.Items`, `Operation.NestedOperations` | Removed &ndash; `Operation.InvalidationCalls` |
| `Operation.ClearEvents()` | `Operation.RemoveEvents()` |
| `FusionBuilder.WithDefaultDeferredInvalidationMode(mode)` | Removed &ndash; declare per handler, or register a `DeferredInvalidationModeResolver` |
| `DeferredInvalidationMode.Any` | Removed &ndash; a handler that defers nothing needs no attribute |
| `RpcArgumentSerializer` | `ArgumentListSerializer`, in `ActualLab.Interception` |
| `OperationCompletion` constructors | `OperationCompletion.New(...)` |
| `ArrayBuffer.MustClean` | `ArrayBuffer.MustClear` |

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
