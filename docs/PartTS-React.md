# @actuallab/fusion-react

React hooks that integrate Fusion's reactive state system with React's rendering lifecycle.
This is the TypeScript equivalent of `ActualLab.Fusion.Blazor`'s `ComputedStateComponent<T>`.

**Peer dependency**: `react ^19`


## useComputedState

The primary hook for real-time data in React. It wraps a `ComputedState<T>` &mdash;
when server-side data changes and invalidations propagate, the component re-renders automatically.

```ts
import { useComputedState } from "@actuallab/fusion-react";

function TodoList({ api }: { api: ITodoApi }) {
  const { value, error, isInitial } = useComputedState(
    () => api.ListIds("~", 10),
    [api],
  );

  if (isInitial) return <p>Loading...</p>;
  if (error) return <p>Error: {String(error)}</p>;

  return (
    <ul>
      {value?.map(id => <li key={id}>{id}</li>)}
    </ul>
  );
}
```

### Signature

```ts
function useComputedState<T>(
  computer: () => T | Promise<T>,
  deps: readonly unknown[],
  options?: ComputedStateOptions<T>,
): UseComputedStateResult<T>;
```

### Parameters

| Parameter | Description |
|-----------|-------------|
| `computer` | The compute function. Runs inside a `ComputeContext`, so any compute method it calls becomes a dependency. Re-runs automatically when dependencies are invalidated. |
| `deps` | React-style dependency array. When deps change, the old `ComputedState` is disposed and a new one is created. |
| `options` | Optional `ComputedStateOptions<T>`: `initialValue`, `initialOutput`, `updateDelayer` |

### Return Value

```ts
interface UseComputedStateResult<T> {
  value: T | undefined;              // undefined until first computation
  error: unknown;                    // error from the last computation
  isInitial: boolean;                // true before first computation completes
  state: ComputedState<T> | undefined; // the underlying state; undefined on the pre-effect first render
}
```

### Lifecycle

The hook is built on React's `useSyncExternalStore`, so updates that land between render and
subscription are never lost and rendering stays tear-free under concurrent React.

1. The `ComputedState<T>` is created and disposed **exclusively inside the store's `subscribe`
   callback** (keyed on `deps`) &mdash; never during render. This is what makes it StrictMode-safe:
   a dev mount &rarr; cleanup &rarr; remount rebuilds a live state instead of freezing on a
   disposed one, and a discarded concurrent render leaks nothing.
2. `subscribe` drives React re-renders from a versioned `state.whenUpdated(sinceIndex)` loop, so no
   generation is missed.
3. On unmount (or when `deps` change) the state is disposed and the update loop stops.

Because the state is created in `subscribe`, `state` is `undefined` on the very first (pre-effect)
render &mdash; `isInitial` is `true` there, so guard on `isInitial` before reaching for `state`.

### Passing AsyncContext

When the `computer` function calls compute methods that themselves call other compute methods,
you may need to capture and pass `AsyncContext` explicitly:

```ts
const { value } = useComputedState(
  () => {
    const ctx = AsyncContext.current;
    return todos.list(10, ctx);
  },
  [todos],
);
```

### With Update Delayer

Control how quickly the state recomputes after invalidation:

```ts
import { FixedDelayer, UIUpdateDelayer } from "@actuallab/fusion";

// Recompute after 500ms delay
const { value } = useComputedState(
  () => api.GetSummary("~"),
  [api],
  { updateDelayer: FixedDelayer.get(500) },
);

// Recompute after 500ms, but right away while UI-action instant updates
// are enabled (a uiActions command is running, or just completed)
const { value } = useComputedState(
  () => api.GetSummary("~"),
  [api],
  { updateDelayer: UIUpdateDelayer.get(500) },
);
```


## useMutableState

A hook that wraps `MutableState<T>` &mdash; a manually-settable reactive value
that re-renders the component on change.

```ts
import { useMutableState } from "@actuallab/fusion-react";

function Counter() {
  const { value: count, set } = useMutableState(0);

  return (
    <div>
      <p>Count: {count}</p>
      <button onClick={() => set(count! + 1)}>+1</button>
    </div>
  );
}
```

### Signature

```ts
function useMutableState<T>(
  initial: T,
): UseMutableStateResult<T>;

interface UseMutableStateResult<T> {
  value: T | undefined;              // never throws, even on an error result
  error: unknown;                    // the error, when the state holds one
  set: (value: Result<T> | T) => void;
  state: MutableState<T>;
}
```

### Return Value

| Field | Type | Description |
|-------|------|-------------|
| `value` | `T \| undefined` | Current value. Reads via `valueOrUndefined`, so it **never throws** even when the state holds an error result |
| `error` | `unknown` | The stored error, if any |
| `set` | `(v) => void` | Setter (accepts `T` or `Result<T>`) |
| `state` | `MutableState<T>` | The underlying state (for use in compute methods via `.use()`) |

Returning `value`/`error` separately (rather than a throwing `value`) means storing an error via
`set(errorResult(e))` re-renders normally instead of throwing during render and unmounting the tree.

The `state` field is useful when you need the `MutableState` to participate
in the Fusion dependency graph &mdash; e.g., as an input to a `@computeMethod` or `useComputedState`:

```ts
function SearchResults({ api }: { api: ISearchApi }) {
  const { value: query, set: setQuery, state: queryState } = useMutableState("");

  const { value: results } = useComputedState(
    () => {
      const q = queryState.use();  // registers dependency
      return q ? api.Search(q) : [];
    },
    [api, queryState],
  );

  return (
    <>
      <input value={query ?? ""} onChange={e => setQuery(e.target.value)} />
      <ul>
        {results?.map(r => <li key={r.id}>{r.title}</li>)}
      </ul>
    </>
  );
}
```


## Re-exports

`@actuallab/fusion-react` re-exports these for convenience:

| Export | From |
|--------|------|
| `UIActionTracker` | `@actuallab/fusion` |
| `uiActions` | `@actuallab/fusion` |
| `UIUpdateDelayer` | `@actuallab/fusion` |


## Connection Status UI

While not part of `@actuallab/fusion-react`, the `RpcPeerStateMonitor` from `@actuallab/rpc`
pairs naturally with React for connection status banners:

```tsx
import { RpcPeerStateMonitor, RpcPeerStateKind, type RpcPeerState } from "@actuallab/rpc";

function ConnectionBanner({ monitor }: { monitor: RpcPeerStateMonitor }) {
  const [state, setState] = React.useState<RpcPeerState>(() => monitor.state);

  React.useEffect(() => {
    const handler = (s: RpcPeerState) => setState(s);
    monitor.stateChanged.add(handler);
    setState(monitor.state);
    return () => monitor.stateChanged.remove(handler);
  }, [monitor]);

  if (state.kind === RpcPeerStateKind.Connected
    || state.kind === RpcPeerStateKind.JustConnected)
    return null;

  const reconnectsIn = state.reconnectsIn > 0
    ? Math.ceil(state.reconnectsIn / 1000) : 0;

  return (
    <div className="alert alert-warning">
      {state.kind === RpcPeerStateKind.JustDisconnected
        ? "Reconnecting..."
        : reconnectsIn > 0
          ? `Disconnected. Reconnecting in ${reconnectsIn}s.`
          : "Reconnecting..."}
      {reconnectsIn > 0 && (
        <button onClick={() => monitor.peer.hub.reconnectDelayer.cancelDelays()}>
          Reconnect now
        </button>
      )}
    </div>
  );
}
```
