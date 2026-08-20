# @actuallab/fusion-react

[![npm](https://img.shields.io/npm/v/@actuallab/fusion-react)](https://www.npmjs.com/package/@actuallab/fusion-react)
[![Documentation](https://img.shields.io/badge/Documentation-6B5B95)](https://fusion.actuallab.net/PartTS-React)
[![License](https://img.shields.io/npm/l/@actuallab/fusion-react)](https://github.com/ActualLab/Fusion/blob/master/LICENSE)

React hooks that plug [Fusion](https://fusion.actuallab.net/)'s reactive states into React's
rendering lifecycle — the TypeScript equivalent of `ComputedStateComponent<T>` in
`ActualLab.Fusion.Blazor`. When server-side data changes and the invalidation reaches the client,
your component re-renders. No polling, no manual subscriptions, no store wiring.

## Installation

```bash
npm install @actuallab/fusion-react
```

Peer dependency: **React ^19**. ESM-first with a CJS fallback; ships its own `.d.ts`.

## useComputedState

```tsx
import { useComputedState } from '@actuallab/fusion-react';

function TodoList({ api }: { api: ITodoApi }) {
    const { value, error, isInitial } = useComputedState(
        () => api.ListIds('~', 10),
        [api],
    );

    if (isInitial) return <p>Loading...</p>;
    if (error) return <p>Error: {String(error)}</p>;
    return <ul>{value?.map(id => <li key={id}>{id}</li>)}</ul>;
}
```

Anything the `computer` calls becomes a dependency, so the hook re-runs when any of it is
invalidated. `deps` works like React's — changing them disposes the old `ComputedState` and builds
a new one.

The hook is built on `useSyncExternalStore`, and the state is created and disposed **inside
`subscribe`**, never during render: StrictMode double-mounts and discarded concurrent renders are
both safe. That's also why `state` is `undefined` on the very first, pre-effect render — guard on
`isInitial`.

Pace recomputation with the `updateDelayer` option:

```tsx
import { FixedDelayer, UIUpdateDelayer } from '@actuallab/fusion';

// 500 ms after invalidation…
useComputedState(() => api.GetSummary('~'), [api], { updateDelayer: FixedDelayer.get(500) });

// …but immediately while a UI action is running (or just finished)
useComputedState(() => api.GetSummary('~'), [api], { updateDelayer: UIUpdateDelayer.get(500) });
```

## useMutableState

A manually-settable reactive value that both re-renders the component and participates in the
Fusion dependency graph:

```tsx
import { useComputedState, useMutableState } from '@actuallab/fusion-react';

function SearchResults({ api }: { api: ISearchApi }) {
    const { value: query, set: setQuery, state: queryState } = useMutableState('');

    const { value: results } = useComputedState(
        () => {
            const q = queryState.use();  // registers the dependency
            return q ? api.Search(q) : [];
        },
        [api, queryState],
    );

    return (
        <>
            <input value={query ?? ''} onChange={e => setQuery(e.target.value)} />
            <ul>{results?.map(r => <li key={r.id}>{r.title}</li>)}</ul>
        </>
    );
}
```

`value` reads through `valueOrUndefined`, so it never throws — an error stored with
`set(errorResult(e))` surfaces as `error` and re-renders normally instead of unmounting the tree.

## API surface

| Export | Description |
|--------|-------------|
| `useComputedState(computer, deps, options?)` | Returns `{ value, error, isInitial, state }` |
| `useMutableState(initial)` | Returns `{ value, error, set, state }` |
| `UIActionTracker`, `uiActions`, `UIUpdateDelayer` | Re-exported from `@actuallab/fusion` for convenience |

For a connection-status banner, pair `RpcPeerStateMonitor` from `@actuallab/rpc` with a plain
`useState` + `useEffect` — see the [docs](https://fusion.actuallab.net/PartTS-React#connection-status-ui).

## Documentation

- [`@actuallab/fusion-react` reference](https://fusion.actuallab.net/PartTS-React) — signatures, lifecycle, `AsyncContext` notes
- [TypeScript port overview](https://fusion.actuallab.net/PartTS)
- [TodoApp sample (React + Fusion)](https://github.com/ActualLab/Fusion.Samples/tree/master/src/TodoApp)

## License

MIT — see [LICENSE](https://github.com/ActualLab/Fusion/blob/master/LICENSE).
