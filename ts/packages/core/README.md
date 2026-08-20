# @actuallab/core

[![npm](https://img.shields.io/npm/v/@actuallab/core)](https://www.npmjs.com/package/@actuallab/core)
[![Documentation](https://img.shields.io/badge/Documentation-6B5B95)](https://fusion.actuallab.net/PartTS-Core)
[![License](https://img.shields.io/npm/l/@actuallab/core)](https://github.com/ActualLab/Fusion/blob/master/LICENSE)

Core primitives shared by every [ActualLab.Fusion](https://fusion.actuallab.net/) TypeScript
package — `Result<T>`, `AsyncContext`, `AsyncLock`, `PromiseSource`, events, retry policies, and
cancellation helpers. It is the TypeScript counterpart of parts of `ActualLab.Core` in .NET, and it
has **zero runtime dependencies**.

You rarely install it directly: `@actuallab/rpc`, `@actuallab/fusion`, `@actuallab/fusion-rpc`, and
`@actuallab/fusion-react` all depend on it. Install it explicitly when you want its primitives on
their own.

## Installation

```bash
npm install @actuallab/core
```

ESM-first with a CJS fallback; ships its own `.d.ts`.

## What's inside

| API | Description |
|-----|-------------|
| `Result<T>`, `result`, `errorResult`, `resultFrom`, `resultFromAsync` | Immutable value-or-error container (.NET's `Result<T>`) |
| `PromiseSource<T>`, `PromiseSourceWithTimeout<T>` | Externally-resolvable promise (.NET's `TaskCompletionSource<T>`) |
| `AsyncContext`, `AsyncContextKey`, `abortSignalKey` | `AsyncLocal<T>`-like typed context; backed by `AsyncLocalStorage` on Node ≥ 20.16 |
| `AsyncLock`, `AsyncSignal` | Promise-based mutual exclusion and signalling |
| `EventHandlerSet<T>` | Typed multicast pub/sub with `whenNext()` |
| `isCancellation`, `cancellationError`, `delayAsync`, `abortPromise`, `awaitWithCleanup` | `AbortSignal`-based cancellation, with .NET's "cancellation is not a failure" semantics |
| `RetryDelaySeq`, `RetryDelayer`, `retry` | Exponential-backoff retry policies |
| `throttle`, `debounce`, `serialize` | Higher-order async operators |
| `RingBuffer`, `DisposableBag` | Small collections / lifetime helpers |
| `Log`, `LogLevel`, `initLogging`, `LogLevelController` | Scoped logging used by all Fusion TS packages |

## Examples

```ts
import { Result, result, errorResult, resultFrom } from '@actuallab/core';

const ok = result(42);
ok.hasValue;          // true
ok.value;             // 42
ok.valueOrUndefined;  // 42 — never throws

const err = errorResult<number>(new Error('fail'));
err.hasError;         // true
err.value;            // throws Error("fail")

resultFrom(() => JSON.parse('{}'));  // Result<any>, exception captured
```

```ts
import { AsyncContext, AsyncContextKey } from '@actuallab/core';

const userKey = new AsyncContextKey<string>('user', 'anonymous');
const ctx = AsyncContext.empty.with(userKey, 'Alice');

ctx.run(() => AsyncContext.current!.get(userKey));  // "Alice"
```

```ts
import { AsyncLock, delayAsync, RetryDelaySeq } from '@actuallab/core';

const lock = new AsyncLock();
await lock.run(async () => { /* critical section */ });

await delayAsync(1000, abortSignal);   // abortable delay
RetryDelaySeq.exp(1000, 30000);        // 1s, 2s, 4s, … capped at 30s
```

## Documentation

- [`@actuallab/core` reference](https://fusion.actuallab.net/PartTS-Core) — full API tables and semantics
- [TypeScript port overview](https://fusion.actuallab.net/PartTS) — architecture, `AsyncContext` rules, .NET differences
- [Fusion documentation](https://fusion.actuallab.net/)

## License

MIT — see [LICENSE](https://github.com/ActualLab/Fusion/blob/master/LICENSE).
