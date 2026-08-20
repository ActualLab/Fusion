# @actuallab/fusion

[![npm](https://img.shields.io/npm/v/@actuallab/fusion)](https://www.npmjs.com/package/@actuallab/fusion)
[![Documentation](https://img.shields.io/badge/Documentation-6B5B95)](https://fusion.actuallab.net/PartTS-Fusion)
[![License](https://img.shields.io/npm/l/@actuallab/fusion)](https://github.com/ActualLab/Fusion/blob/master/LICENSE)

The TypeScript implementation of [Fusion](https://fusion.actuallab.net/)'s core: memoized
computations with automatic dependency tracking and cascading invalidation. It gives you
`Computed<T>`, the `@computeMethod` decorator, `ComputedState<T>`, `MutableState<T>`, and
`UIActionTracker` — the same abstractions Fusion provides on .NET.

This package works standalone (client-side reactive state, no server needed). Add
[`@actuallab/fusion-rpc`](https://www.npmjs.com/package/@actuallab/fusion-rpc) to consume
invalidation-aware Compute Services from a .NET server, and
[`@actuallab/fusion-react`](https://www.npmjs.com/package/@actuallab/fusion-react) to render them
in React.

## Installation

```bash
npm install @actuallab/fusion
```

ESM-first with a CJS fallback; ships its own `.d.ts`. Depends only on `@actuallab/core`.

## Compute methods

`@computeMethod` is the equivalent of .NET's `[ComputeMethod]` — it wraps a method with caching and
dependency tracking. Results are keyed by `JSON.stringify` of the arguments.

```ts
import { computeMethod } from '@actuallab/fusion';

class CounterService {
    private _counters = new Map<string, number>();

    @computeMethod
    async get(key: string): Promise<number> {
        return this._counters.get(key) ?? 0;
    }

    @computeMethod
    async sum(key1: string, key2: string): Promise<number> {
        // sum() automatically depends on get(key1) and get(key2)
        return (await this.get(key1)) + (await this.get(key2));
    }

    increment(key: string): void {
        this._counters.set(key, (this._counters.get(key) ?? 0) + 1);
        (this.get as any).invalidate(key);  // cascades into sum()
    }
}
```

There is no `Invalidation.Begin()` block here: every bound compute method carries an
`.invalidate(...args)` function. For standalone functions, use `wrapComputeMethod`.

## Reactive states

`ComputedState<T>` recomputes itself whenever anything it used gets invalidated;
`MutableState<T>` is set by hand and participates in the same dependency graph.

```ts
import { ComputedState, MutableState, FixedDelayer } from '@actuallab/fusion';

const state = new ComputedState(
    async () => `Count: ${await counters.get('a')}`,
    { initialValue: 'loading...', updateDelayer: FixedDelayer.get(500) },
);

await state.whenFirstTimeUpdated();
state.value;     // "Count: 0" — and it updates on its own from here on
state.dispose(); // required: stops the update loop

const query = new MutableState('');
query.set('fusion');  // invalidates every computation that called query.use()
```

## API surface

| API | Description |
|-----|-------------|
| `Computed<T>`, `ConsistencyState` | Cached computation result: `.value`, `.update()`, `.use()`, `.invalidate()`, `.whenInvalidated()` |
| `Computed.capture(fn)` | Capture the `Computed<T>` a compute call produced |
| `computeMethod`, `wrapComputeMethod` | Turn a method / function into a compute function |
| `ComputedOptions` | Per-method options, e.g. `errorAutoInvalidateDelay` |
| `ComputedRegistry`, `ComputeFunction`, `ComputeContext` | The kernel, for advanced/custom integrations |
| `State<T>`, `ComputedState<T>`, `MutableState<T>` | Reactive states |
| `UpdateDelayer`, `FixedDelayer`, `UIUpdateDelayer`, `defaultUpdateDelayer` | Recompute pacing (≈32 ms floor, retry backoff) |
| `UIActionTracker`, `uiActions` | Tracks running UI commands, collects errors, enables instant updates |

## Notes

- **Dependency tracking across `await`.** On Node ≥ 20.16 `AsyncContext` is backed by
  `AsyncLocalStorage`, so it just works. In browsers the child `AsyncContext` is passed to your
  compute method as a trailing argument — accept it and forward it into nested compute calls. See
  [AsyncContext: Why It Matters](https://fusion.actuallab.net/PartTS#asynccontext-why-it-matters).
- **Dispose your `ComputedState`s.** Otherwise their update loops keep running.
- **Errors.** Compute methods auto-invalidate an error output after 1 s by default (configurable
  via `@computeMethod({ errorAutoInvalidateDelay })`); states never do — they retry with backoff.
  Cancellation-shaped errors are never cached.

## Documentation

- [`@actuallab/fusion` reference](https://fusion.actuallab.net/PartTS-Fusion) — full API tables and semantics
- [TypeScript port overview](https://fusion.actuallab.net/PartTS)
- [Compute Services](https://fusion.actuallab.net/PartF) and [States](https://fusion.actuallab.net/PartF-ST) — the concepts, in .NET terms
- [TodoApp sample (React + Fusion)](https://github.com/ActualLab/Fusion.Samples/tree/master/src/TodoApp)

## License

MIT — see [LICENSE](https://github.com/ActualLab/Fusion/blob/master/LICENSE).
