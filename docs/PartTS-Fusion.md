# @actuallab/fusion

The core Fusion package for TypeScript &mdash; provides `Computed<T>`, `@computeMethod`,
`ComputedState<T>`, `MutableState<T>`, and `UIActionTracker`.

These are the TypeScript equivalents of the types documented in
[Part 1: Compute Services](./PartF.md) and [States](./PartF-ST.md).


## Computed\<T\>

`Computed<T>` is the fundamental building block &mdash; a cached computation result
with dependency tracking and invalidation.
See [Computed\<T\> in .NET](./PartF-C.md) for the full conceptual overview.

### Key Differences from .NET

| Aspect | .NET | TypeScript |
|--------|------|------------|
| Dependency tracking | Via `AsyncLocal` / `ExecutionContext` (automatic across `await`) | Via `AsyncContext.current` &mdash; automatic on Node ≥ 20.16 (`AsyncLocalStorage`), explicit trailing-`ctx` threading in browsers |
| Registry | `ComputedRegistry` with GC handle tracking | `ComputedRegistry` using `WeakRef` + `FinalizationRegistry` |
| Error auto-invalidation | Per-method via `ComputedOptions` | Per-method via `ComputedOptions` (`errorAutoInvalidateDelay`; compute methods 1 s, states `Infinity`) |

### API

```ts
import { Computed, ConsistencyState } from "@actuallab/fusion";

// Capture the Computed backing a compute method call
const c = await Computed.capture(() => myService.getCount());

c.isConsistent;   // true
c.value;          // the cached value
c.state;          // ConsistencyState.Consistent

// Wait for invalidation, then update
await c.whenInvalidated();
const c2 = await c.update();  // returns new Computed<T> if inconsistent

// Manual invalidation
c.invalidate();   // cascades to all dependants
```

| Member | Description |
|--------|-------------|
| `Computed.capture(fn)` | Static: capture the `Computed<T>` produced by `fn`. **Last-wins** if `fn` makes several top-level compute calls; returns the errored `Computed` (rather than throwing) when the captured computation failed with a non-cancellation error |
| `.input` | The identity key (`string` for compute functions, `State` for state-bound) |
| `.options` | The `ComputedOptions` for this computed (carries `errorAutoInvalidateDelay`) |
| `.version` | Monotonically increasing version number |
| `.state` | `ConsistencyState`: `Computing`, `Consistent`, or `Invalidated` |
| `.isConsistent` / `.isComputing` | Shorthands for the current `state` |
| `.value` / `.error` / `.hasValue` / `.hasError` | `IResult<T>` implementation |
| `.valueOrUndefined` | Value or `undefined` (never throws) |
| `.output` | The `Result<T>` output |
| `.update()` | Returns `this` if consistent, otherwise recomputes. Returns `Computed<T> \| Promise<Computed<T>>` |
| `.use(ctx?)` | Update + register as dependency of the current computation |
| `.useInconsistent(ctx?)` | Register as dependency without updating (may return stale value) |
| `.invalidate()` | Invalidate this computed and all dependants (cascading). **Never throws** &mdash; handler exceptions are isolated and logged |
| `.whenInvalidated(signal?)` | `Promise<void>` that resolves on invalidation; **rejects promptly** if `signal` is (or becomes) aborted |
| `.onInvalidated(handler)` | **Method**: subscribe to invalidation. If the computed is *already* invalidated, `handler` fires immediately (exactly-once regardless of subscription timing) |
| `.setOutput(result)` | Set the output (only valid during `Computing` state) |
| `.addDependency(dep)` | Add a dependency edge (no-op unless `this` is still `Computing`; if `dep` is already invalidated, invalidates `this` instead) |

### ConsistencyState

```ts
enum ConsistencyState {
  Computing = 0,   // Being computed right now
  Consistent = 1,  // Has a valid, up-to-date output
  Invalidated = 2, // Output is stale
}
```


## ComputedOptions

Per-`Computed` configuration &mdash; the TypeScript analog of .NET's `ComputedOptions`. Every
`Computed` carries one, resolved from its `ComputeFunction` (per-declaration override) or its kind's
static default. Today it holds a single field, `errorAutoInvalidateDelay` (ms), with more per-method
knobs planned to land here rather than as new mechanisms.

```ts
import { ComputedOptions } from "@actuallab/fusion";

ComputedOptions.default;            // compute-method default: errorAutoInvalidateDelay = 1000 (1 s)
ComputedOptions.mutableStateDefault; // state-bound default:   errorAutoInvalidateDelay = Infinity
```

- **Compute methods** default to a finite `errorAutoInvalidateDelay` (1 s): an error output is
  auto-invalidated after the delay, so the method retries. Override per method with
  `@computeMethod({ errorAutoInvalidateDelay })`.
- **State-bound computeds** (`ComputedState` / `MutableState`) default to `Infinity` &mdash; a state
  holding an error **never** auto-invalidates on a timer. `ComputedState` re-attempts via its own
  update loop with backoff instead (see `UpdateDelayer`).
- A **cancellation-shaped error** (`isCancellation`, e.g. an aborted `fetch`) is never cached: the
  computed is invalidated immediately regardless of `errorAutoInvalidateDelay`.

::: tip
The old global `Computed.errorAutoInvalidateDelay` is gone. Configure the delay per compute method
via the decorator, or rely on the per-kind defaults above.
:::


## @computeMethod Decorator

The `@computeMethod` decorator wraps a method with caching and dependency tracking &mdash;
equivalent to `[ComputeMethod]` + `virtual` in .NET.

```ts
import { computeMethod } from "@actuallab/fusion";

class CounterService {
  private _counters = new Map<string, number>();

  @computeMethod
  async get(key: string): Promise<number> {
    return this._counters.get(key) ?? 0;
  }

  @computeMethod
  async sum(key1: string, key2: string): Promise<number> {
    // Automatic dependency: sum depends on get(key1) and get(key2)
    return await this.get(key1) + await this.get(key2);
  }

  increment(key: string): void {
    this._counters.set(key, (this._counters.get(key) ?? 0) + 1);
    // Invalidate — triggers cascading invalidation of sum() etc.
    (this.get as any).invalidate(key);
  }
}
```

### Invalidation

In .NET, you invalidate via `Invalidation.Begin()` blocks.
In TypeScript, each bound method gets an `.invalidate(...args)` function:

```ts
const svc = new CounterService();
await svc.get("a");  // Computed, cached

// Invalidate the cached result for get("a")
(svc.get as any).invalidate("a");

await svc.get("a");  // Recomputed
```

### Options

`@computeMethod` is usable bare (`@computeMethod`) or with an options object
(`@computeMethod({ ... })`) carrying `ComputedOptions` fields plus `argCount`:

```ts
class PriceService {
  // Cache errors for 5 s instead of the 1 s default before retrying.
  @computeMethod({ errorAutoInvalidateDelay: 5000 })
  async quote(symbol: string): Promise<number> { /* ... */ }

  // Give a defaulted/rest-parameter method an explicit wire arity.
  @computeMethod({ argCount: 2 })
  async range(from: number, to = 100): Promise<number[]> { /* ... */ }
}
```

The argument count is normally taken from `Function.length`. Because JavaScript's `Function.length`
stops at the first defaulted or rest parameter, a method that has one **throws at declaration time**
unless you pass an explicit `argCount`.

### Argument Keying

Cached results are keyed by `JSON.stringify` of each argument, so arguments that don't round-trip
through JSON can **collide on the same cache key**: functions, `undefined`, and symbols all map to
the same key component, `Map`/`Set` and objects without serializable properties map to `{}`, and
`NaN` maps to `null`. The compute method's author owns keyability &mdash; stick to
JSON-representable argument types, or build on `ComputeFunction` directly and supply a custom
`argToString`.

### wrapComputeMethod

For standalone functions (not class methods), use `wrapComputeMethod`:

```ts
import { wrapComputeMethod } from "@actuallab/fusion";

const getTime = wrapComputeMethod(function getTime(): number {
  return Date.now();
});

const t1 = await getTime();  // computed + cached
const t2 = await getTime();  // cache hit (t1 === t2)

getTime.invalidate();
const t3 = await getTime();  // recomputed
```


## State\<T\>

Abstract base class for all reactive state types.
See [States in .NET](./PartF-ST.md) for the full conceptual overview.

| Member | Description |
|--------|-------------|
| `.computed` | The current `Computed<T>` backing this state |
| `.value` / `.error` / `.hasValue` / `.hasError` | `IResult<T>` delegation to `.computed` |
| `.valueOrUndefined` | Value or `undefined` (never throws) |
| `.output` | The `Result<T>` output |
| `.lastNonErrorValue` | Last value that was not an error |
| `.updateIndex` | How many times the state has been updated |
| `.use(ctx?)` | Use the state in a computation (registers dependency) |
| `.useInconsistent(ctx?)` | Use without updating |
| `.update()` | Ensure the backing computed is up-to-date |
| `.recompute()` | Invalidate + update |
| `.whenInvalidated()` | Promise that resolves when the current computed is invalidated |
| `.whenUpdated(sinceIndex?)` | Versioned wait: resolves at once if `updateIndex` already moved past `sinceIndex` (defaults to the current index, so no generation is missed); **rejects** once the state is disposed |
| `.whenFirstTimeUpdated()` | `whenUpdated(0)` &mdash; resolves immediately if already updated, otherwise waits |


## ComputedState\<T\>

Auto-updating reactive state &mdash; re-computes on invalidation with configurable delay.
This is the TypeScript equivalent of `ComputedState<T>` in .NET.

```ts
import { ComputedState, FixedDelayer } from "@actuallab/fusion";

const state = new ComputedState(
  async () => {
    // This is the "compute method" for this state
    const count = await counterService.get("a");
    return `Count: ${count}`;
  },
  {
    initialValue: "loading...",
    updateDelayer: FixedDelayer.get(500),  // 500ms delay after invalidation
  },
);

// Wait for first computation
await state.whenFirstTimeUpdated();
console.log(state.value);  // "Count: 0"

// After counterService.get("a") is invalidated:
// - state auto-recomputes after 500ms delay
// - state.value updates to "Count: 1"

// Clean up (stops the update loop)
state.dispose();
```

::: warning
`ComputedState` instances **must be disposed** when no longer needed.
Otherwise the update loop runs indefinitely.
:::

### Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `initialValue` | `T` | &mdash; | Value before first computation completes |
| `initialOutput` | `Result<T>` | &mdash; | Full result before first computation |
| `updateDelayer` | `UpdateDelayer` | `defaultUpdateDelayer` (one frame, floored to 32 ms) | Delay before recomputation after invalidation |

The initial computed is created **pre-invalidated**, so calling `use()`/`update()`/`recompute()` on
a freshly-constructed `ComputedState` (before its first computation) awaits the first real value
instead of throwing. `dispose()` is prompt: an in-flight computation is dropped without publishing,
and any pending `whenUpdated()`/`update()` waiters reject rather than hang.

### UpdateDelayer

An `UpdateDelayer` is `(retryCount: number, abortSignal?: AbortSignal) => Promise<void>` &mdash; the
delay can grow with the count of consecutive failed computations, and the wait is abortable.

```ts
import { FixedDelayer, defaultUpdateDelayer } from "@actuallab/fusion";

FixedDelayer.zero;      // The only genuinely-zero delayer
FixedDelayer.get(1000); // 1-second delay (floored to a 32 ms minimum)

// Default update delayer: one frame (1000/60 ≈ 16 ms), floored to the 32 ms minimum
defaultUpdateDelayer;
```

- **Minimum floor.** Every delayer except `FixedDelayer.zero` enforces a ~32 ms minimum delay
  (`FixedDelayer.get(0)` becomes 32 ms), so a state whose dependency invalidates on every recompute
  can't spin the CPU.
- **Retry backoff.** On consecutive errors the delay follows a `RetryDelaySeq` growing from 1 s to
  1 min (mirroring .NET's `RetryDelays`); the counter resets on the first success. This is why
  states no longer need a blanket 1 s error auto-invalidation.
- **Abort resolves, never rejects.** When the state is disposed mid-wait the delayer resolves (the
  loop re-checks the dispose signal), so a disposed state produces no unhandled rejection.


## MutableState\<T\>

Manually-settable reactive state &mdash; the TypeScript equivalent of `MutableState<T>`.

```ts
import { MutableState } from "@actuallab/fusion";

const state = new MutableState(0);

state.value;  // 0

state.set(42);
state.value;  // 42

// Can also set an error
import { errorResult } from "@actuallab/core";
state.set(errorResult(new Error("oops")));
state.error;            // Error("oops")
state.lastNonErrorValue; // 42 (preserved from last successful value)
```

`MutableState<T>` participates in the dependency graph:
if a `@computeMethod` calls `state.use()`, it becomes dependent on the state
and will be invalidated when the state changes.

`set(output)` mirrors .NET's `MutableState.Set`:

- **Equality short-circuit** &mdash; setting an output equal to the current one (value via `Object.is`,
  error by reference) is a no-op, so no cascade or re-render fires.
- **Stage-then-invalidate** &mdash; the new output is staged before the invalidation cascade runs, so
  a reader that reacts synchronously to the invalidation already observes the new value.
- **Synchronous renewal** &mdash; the backing computed is renewed synchronously on invalidation, so a
  `MutableState` is never observed in an invalidated state.

Setting an **error** result is fully supported and `set` itself never throws; reading `.value` then
throws the stored error (use `.valueOrUndefined`/`.error` instead), and `.lastNonErrorValue` returns
the last successful value.


## UIActionTracker

Singleton that tracks active UI commands &mdash; the TypeScript equivalent of
Fusion's `UIActionTracker` + `UICommander`.

```ts
import { uiActions } from "@actuallab/fusion";

// Run a command — errors collected, not thrown
await uiActions.run(async () => {
  await api.AddOrUpdate({ session: "~", item: newTodo });
});

// Call a command — errors collected AND thrown
const result = await uiActions.call(async () => {
  return await api.Get("~", id);
});

// Check state
uiActions.isActive;     // true while any command is running
uiActions.errors;       // collected errors
uiActions.dismissError(0);  // remove first error

// Subscribe to changes
uiActions.changed.add(() => {
  // re-render UI
});

// Instant-updates window: true while an action runs OR within instantUpdatePeriod
// (300ms) after the last one completed.
uiActions.areInstantUpdatesEnabled();
```

The error list is bounded: a failure whose name+message matches a recent one (within
`maxDuplicateRecency`, 1 s) is dropped, and the list is capped at `maxErrors` (100, oldest evicted).
So a retrying failure won't flood a toast UI or grow memory for the session's lifetime.

### UIUpdateDelayer

An `UpdateDelayer` that short-circuits the delay while `UIActionTracker` enables **instant updates**,
so states recompute immediately in response to user actions:

```ts
import { UIUpdateDelayer } from "@actuallab/fusion";

const delayer = UIUpdateDelayer.get(500);  // returns an UpdateDelayer function
// Normal: waits 500ms after invalidation before recomputing
// While instant updates are enabled (a UI action is running, or within 300ms
// of the last one completing): recomputes right away.
```

Instant updates are gated on `uiActions.areInstantUpdatesEnabled()` &mdash; not merely
`isActive` &mdash; so an invalidation arriving just *after* a command completes still refreshes
promptly. Even on the instant path a ~32 ms minimum delay is enforced (measured from the delay's
start) to prevent a hot recompute loop during a long-running action.
