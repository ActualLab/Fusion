# @actuallab/core

Core primitives used across all other Fusion TypeScript packages.
This package has **zero dependencies** and corresponds to parts of `ActualLab.Core` in .NET.


## Result\<T\>

Immutable value-or-error container &mdash; the TypeScript equivalent of `Result<T>` in .NET.

```ts
import { Result, result, errorResult, resultFrom, resultFromAsync } from "@actuallab/core";

const ok = result(42);            // Result<number> with value
const err = errorResult<number>(new Error("fail"));  // Result<number> with error

ok.hasValue;        // true
ok.value;           // 42
ok.error;           // undefined
ok.valueOrUndefined // 42

err.hasError;       // true
err.error;          // Error("fail")
err.value;          // throws Error("fail")

// Wrap a function call in a Result
const r1 = resultFrom(() => JSON.parse("{}"));          // Result<any>
const r2 = await resultFromAsync(() => fetch("/api"));   // Result<Response>
```

| Member | Description |
|--------|-------------|
| `result(value, error?)` | Create a `Result` from value and optional error |
| `errorResult(error)` | Create an error `Result` |
| `resultFrom(fn)` | Wrap a sync function &mdash; catches exceptions into error result |
| `resultFromAsync(fn)` | Wrap an async function &mdash; catches rejections into error result |
| `.hasValue` | `true` if the result holds a value |
| `.hasError` | `true` if the result holds an error |
| `.value` | Returns value or **throws** the stored error |
| `.valueOrUndefined` | Returns value or `undefined` (never throws) |
| `.error` | Returns the error or `undefined` |
| `.equals(other, valueComparer?)` | Structural equality &mdash; values via `valueComparer` (`Object.is` by default), errors by reference |

`hasValue`/`hasError` are independent, unforgeable flags decided when the `Result` is constructed
(not derived from whether the error is nullish), so an error result whose error happens to be
`undefined`/`null` is still an error. `errorResult`/`resultFrom*` normalize a nullish error to
`new Error('Unspecified error')`, so `throw undefined` never turns into a "successful" result.


## PromiseSource\<T\>

Externally-resolvable promise &mdash; the TypeScript equivalent of `TaskCompletionSource<T>`.

```ts
import { PromiseSource } from "@actuallab/core";

const ps = new PromiseSource<string>();
ps.isCompleted;  // false

// Somewhere else:
ps.resolve("done");
// or: ps.reject(new Error("fail"));

const value = await ps.promise;  // "done"
ps.isCompleted;  // true
```

| Member | Description |
|--------|-------------|
| `.promise` | The underlying `Promise<T>` |
| `.isCompleted` | `true` after `resolve` or `reject` |
| `.resolve(value)` | Resolve the promise |
| `.reject(error)` | Reject the promise |

There is also a pre-resolved constant: `resolvedVoidPromise`.


## AsyncContext

General-purpose typed context container &mdash; provides `AsyncLocal<T>`-like functionality
for JavaScript's single-threaded environment. Named for forward-compatibility with the
[TC39 AsyncContext proposal](https://github.com/nicolo-ribaudo/proposal-async-context/).

```ts
import { AsyncContext, AsyncContextKey } from "@actuallab/core";

// Define a typed key
const userKey = new AsyncContextKey<string>("user", "anonymous");

// Create context with a value
const ctx = AsyncContext.empty.with(userKey, "Alice");

// Run code within the context
ctx.run(() => {
  AsyncContext.current!.get(userKey);  // "Alice"
});

// Or activate/deactivate manually
const guard = ctx.activate();
AsyncContext.current!.get(userKey);  // "Alice"
guard.dispose();
```

| Member | Description |
|--------|-------------|
| `AsyncContext.current` | Static: the currently active context (or `undefined`) |
| `AsyncContext.empty` | Static: immutable empty singleton |
| `AsyncContext.from(ctx)` | Returns `ctx` if defined, otherwise `.current` |
| `AsyncContext.fromArgs(args)` | Extract context from last argument, or fall back to `.current` |
| `.get(key)` | Read a value by key (returns `key.defaultValue` if not set) |
| `.with(key, value)` | Create a new context with the key set (immutable) |
| `.run(fn)` | Execute `fn` with this context as `.current` |
| `.activate()` | Set as `.current`, return a `Disposable` that restores the previous one |
| `.stripFromArgs(args)` | Remove this context from the end of an args array |

Additional statics: `AsyncContext.isAsyncLocalStorageActive` (`true` when the Node
`AsyncLocalStorage` backing is active), `AsyncContext.getOrCreate()`, and
`AsyncContext.setDefault(key, value)`. The exported `abortSignalKey` is the well-known key the
RPC/compute layers use to carry a caller's `AbortSignal` through the context.

::: warning
On **Node ≥ 20.16** `AsyncContext` is backed by `AsyncLocalStorage`, so `AsyncContext.current`
flows across `await` boundaries automatically (like C#'s `AsyncLocal`). In **browsers** it does
not: after the first `await` the ambient context is lost, so compute methods receive the child
`AsyncContext` as a trailing argument and must forward it into nested calls.
See [AsyncContext: Why It Matters](./PartTS.md#asynccontext-why-it-matters).
:::


## AsyncLock

Promise-based mutual exclusion lock &mdash; the TypeScript equivalent of `AsyncLock`.

```ts
import { AsyncLock } from "@actuallab/core";

const lock = new AsyncLock();

// Option 1: acquire/release
await lock.acquire();
try {
  // critical section
} finally {
  lock.release();
}

// Option 2: run (acquires, runs fn, releases)
await lock.run(async () => {
  // critical section
});

// Both acquire() and run() accept an optional AbortSignal — an already-aborted
// signal rejects immediately, and a queued waiter is removed and rejected with
// signal.reason when the signal fires.
await lock.acquire(abortSignal);
```


## EventHandlerSet\<T\>

Typed pub/sub event system &mdash; similar to .NET's multicast delegates.

```ts
import { EventHandlerSet } from "@actuallab/core";

const onChanged = new EventHandlerSet<string>();

const handler = (msg: string) => console.log(msg);
onChanged.add(handler);

onChanged.trigger("hello");  // logs "hello"
onChanged.remove(handler);

// Await the next event
const next = await onChanged.whenNext();
```

| Member | Description |
|--------|-------------|
| `.add(handler)` | Subscribe |
| `.remove(handler)` | Unsubscribe |
| `.trigger(value)` | Fire all handlers synchronously, over a **snapshot** &mdash; handlers added during dispatch don't run in that dispatch (multicast-delegate parity) |
| `.triggerSafe(value, onError)` | Like `trigger`, but each handler is isolated so one that throws can't stop the rest |
| `.clear()` | Remove all handlers |
| `.count` | Number of subscribed handlers |
| `.whenNext()` | Returns a `Promise` that resolves on the **next** trigger |

::: tip
Because `trigger` iterates a snapshot, `whenNext()` called from inside a handler resolves on the
*next* event, not the one being dispatched. `EventHandlerSet` is `Set`-backed, so adding the same
handler twice still fires it once (a C# multicast delegate would fire it twice).
:::


## RetryDelaySeq and RetryDelayer

Retry delay sequences with exponential backoff, similar to .NET's `RetryDelaySeq`.

```ts
import { RetryDelaySeq, RetryDelayer } from "@actuallab/core";

// Fixed 1-second delays
const fixed = RetryDelaySeq.fixed(1000);

// Exponential: 1s, 2s, 4s, ... capped at 30s
const exp = RetryDelaySeq.exp(1000, 30000);

// RetryDelayer wraps a sequence with cancellation support
const delayer = new RetryDelayer();
delayer.delays = exp;
delayer.limit = 10;  // max 10 retries
const delay = delayer.getDelay(3);  // 3rd retry
if (!delay.isLimitExceeded) {
  await delay.promise;  // wait for the delay
}
```


## Cancellation & Abort Helpers

`AbortSignal` is the port's `CancellationToken` analog, and these helpers give it the same
"cancellation is not a failure" semantics as .NET:

```ts
import {
  isCancellation, cancellationError,
  delayAsync, awaitWithCleanup, retry, abortPromise,
} from "@actuallab/core";

// Recognize / construct cancellation-shaped errors (named "AbortError").
isCancellation(err);              // true for aborted fetch, AsyncLock/delay rejections, etc.
throw cancellationError();        // an Error the compute kernel never caches (see Computed)

// Cancellable delay — pre-checks signal.aborted, clears the timer on abort,
// rejects with signal.reason.
await delayAsync(1000, abortSignal);

// retry() also accepts an AbortSignal so inter-attempt delays can be cancelled.
```

- `isCancellation(error)` &mdash; `true` when `error` is cancellation-shaped (any object named
  `'AbortError'`, including an aborted `fetch`'s `DOMException`).
- `cancellationError(message?)` &mdash; builds such an error. Cancellation-shaped errors are
  **never cached** by the compute kernel and are treated as retryable by RPC callers.
- `delayAsync(ms, signal?)` &mdash; abortable delay.
- `awaitWithCleanup(signal, abortMode, body)` &mdash; the shared "await with guaranteed cleanup"
  primitive the delayers are built on; removes every listener/timer on every exit path.
- `abortPromise(signal)` &mdash; a cached, pre-observed rejecting promise per signal (safe to hold
  and race later without an unhandled rejection).

`PromiseSource` rejections are safe when unobserved: the constructor pre-attaches a no-op `.catch()`,
so rejecting a source nobody is awaiting will not raise `unhandledrejection` (the .NET
"unobserved faulted task is benign" contract).


## Decorator Helpers

Small utilities shared by the `@computeMethod`, `@rpcService`, and `@rpcMethod` decorators:

| Helper | Description |
|--------|-------------|
| `ownMetadata(metadata, key)` | Returns the class's **own** metadata record for `key`, cloning an inherited one first &mdash; so decorating a derived class never mutates a base class's contract |
| `resolveArgCount(fn, explicit, methodName)` | Resolves a method's wire argument count (`explicit` may be `undefined`); throws at declaration time when `fn` has a defaulted or rest parameter (ambiguous `Function.length`) unless an explicit count is given |


## DisposableBag

Aggregates multiple `Disposable` objects and disposes them in LIFO order.

```ts
import { DisposableBag } from "@actuallab/core";

const bag = new DisposableBag();
bag.add({ dispose: () => console.log("first") });
bag.add({ dispose: () => console.log("second") });
bag.dispose();  // logs "second", then "first"
```
