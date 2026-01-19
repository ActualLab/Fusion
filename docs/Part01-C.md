# Computed&lt;T&gt;: The Core Abstraction

This document covers `Computed<T>`, `IComputed`, extension methods, `ComputedRegistry`, and invalidation source tracking.

## Overview

`Computed<T>` is Fusion's central abstraction — an immutable, cached result of a computation that:

- Stores either a value or an error
- Tracks its consistency state (Computing → Consistent → Invalidated)
- Knows its dependencies and dependants
- Can notify when it becomes invalidated
- Can produce an updated version of itself

## IComputed Interface

```csharp
public interface IComputed : IResult, IHasVersion<ulong>
{
    ComputedOptions Options { get; }
    ComputedInput Input { get; }
    Type OutputType { get; }
    ConsistencyState ConsistencyState { get; }
    Result Output { get; }
    InvalidationSource InvalidationSource { get; }

    event Action<Computed> Invalidated;

    void Invalidate(bool immediately = false, ...);
    ValueTask<Computed> UpdateUntyped(CancellationToken cancellationToken = default);
    Task UseUntyped(CancellationToken cancellationToken = default);
}
```

## Computed&lt;T&gt; Properties

### Value Access

```csharp
T Value { get; }           // Throws if Error is set
T? ValueOrDefault { get; } // Returns default(T) if Error is set
Exception? Error { get; }  // The error, if computation failed
bool HasValue { get; }     // True if no error
bool HasError { get; }     // True if error

// Deconstruct for pattern matching
var (value, error) = computed;
var (value, error, version) = computed;
```

### Metadata

```csharp
ulong Version { get; }              // Unique version number
ComputedOptions Options { get; }    // Configuration options
ComputedInput Input { get; }        // The input that produced this computed
ConsistencyState ConsistencyState { get; }  // Current state
```

### ConsistencyState

Every `Computed<T>` goes through these states:

| State | Description |
|-------|-------------|
| `Computing` | Currently being computed (mutable during this phase) |
| `Consistent` | Computation complete, value is current |
| `Invalidated` | Marked as outdated, needs recomputation |

```csharp
// Check state via extension methods
computed.IsConsistent()    // true if Consistent
computed.IsInvalidated()   // true if Invalidated
computed.IsComputing()     // true if Computing
computed.IsConsistentOrComputing()  // true if not Invalidated
```

## Static Methods

### Computed.Current

Access the currently computing `Computed<T>` from within a compute method:

```csharp
[ComputeMethod]
public virtual async Task<Data> GetData()
{
    var current = Computed.Current;  // The Computed<Data> being built
    // or
    var current = Computed.GetCurrent<Data>();  // Throws if null
}
```

### Computed.Capture

Capture the `Computed<T>` produced by a compute method call:

```csharp
var computed = await Computed.Capture(() => service.GetData());
// computed is Computed<Data>

// TryCapture returns null if capture fails
var computed = await Computed.TryCapture(() => service.GetData());
```

### Computed.GetExisting

Get the cached `Computed<T>` without triggering computation:

```csharp
var existing = Computed.GetExisting(() => service.GetData());
// Returns null if not cached, never computes
```

### Computed.New

Create a standalone computed value (useful for tests):

```csharp
var computed = Computed.New(async ct => {
    await Task.Delay(100, ct);
    return 42;
});
```

### Context Scopes

```csharp
// Isolate from dependency tracking
using (Computed.BeginIsolation()) {
    // Calls here won't register as dependencies
}

// Capture mode
using var scope = Computed.BeginCapture();
await service.GetData();
var captured = scope.Context.GetCaptured<Data>();
```

## Extension Methods

### Update & Use

```csharp
// Get the latest version (recomputes if invalidated)
Computed<T> updated = await computed.Update(ct);

// Use as a dependency in current computation
T value = await computed.Use(ct);

// Use even if inconsistent (for showing stale data)
T value = await computed.Use(allowInconsistent: true, ct);
```

### Invalidation

```csharp
// Invalidate immediately
computed.Invalidate();
computed.Invalidate(immediately: true);

// Invalidate after delay
computed.Invalidate(TimeSpan.FromSeconds(30));

// With explicit source for debugging
computed.Invalidate(new InvalidationSource("MyReason"));
```

### Waiting for Changes

```csharp
// Wait until invalidated
await computed.WhenInvalidated(ct);

// Wait until value satisfies predicate
var updated = await computed.When(x => x > 100, ct);

// With custom update delayer
var updated = await computed.When(
    x => x > 100,
    FixedDelayer.Get(1),  // 1 second between checks
    ct);
```

### Observing Changes

```csharp
// Stream of computed values as they change
await foreach (var c in computed.Changes(ct)) {
    Console.WriteLine($"New value: {c.Value}");
}

// With custom delayer
await foreach (var c in computed.Changes(FixedDelayer.Get(0.5), ct)) {
    // ...
}
```

### Invalidation Chain Navigation

```csharp
// Get the root cause of invalidation
Computed origin = computed.GetInvalidationOrigin();
```

## Invalidation Event

Subscribe to know when a computed becomes invalidated:

```csharp
computed.Invalidated += c => {
    Console.WriteLine($"Invalidated: {c}");
};

// If already invalidated, handler fires immediately
```

## ComputedRegistry

`ComputedRegistry` is the global cache of all `Computed<T>` instances.

### Accessing the Registry

```csharp
// Get cached computed by input
Computed? cached = ComputedRegistry.Get(input);

// Enumerate all cached inputs
foreach (var input in ComputedRegistry.Keys) { ... }

// Invalidate everything (useful for tests)
ComputedRegistry.InvalidateEverything();

// Force prune dead entries
await ComputedRegistry.Prune();
```

### Events

```csharp
// Called when a new Computed is registered
ComputedRegistry.OnRegister += computed => { ... };

// Called when a Computed is unregistered (after invalidation)
ComputedRegistry.OnUnregister += computed => { ... };

// Called on every access (for monitoring)
ComputedRegistry.OnAccess += (computed, isNew) => { ... };
```

### Configuration

```csharp
// Before first use of Fusion:
ComputedRegistry.Settings.InitialCapacity = 10000;
ComputedRegistry.Settings.ConcurrencyLevel = 64;
```

### Metrics

`ComputedRegistry.Metrics` exposes OpenTelemetry metrics:
- `computed.registry.key.count` — number of cached entries
- `computed.registry.node.count` — nodes in dependency graph
- `computed.registry.edge.count` — edges in dependency graph
- `computed.registry.pruned.*` — pruning statistics

## Invalidation Source Tracking

Fusion can track *why* a computed was invalidated, which is invaluable for debugging.

### InvalidationSource

Every invalidated `Computed<T>` has an `InvalidationSource`:

```csharp
if (computed.IsInvalidated()) {
    var source = computed.InvalidationSource;
    Console.WriteLine($"Invalidated by: {source}");
}
```

### Tracking Modes

Configure via `Invalidation.TrackingMode`:

```csharp
// At startup:
Invalidation.TrackingMode = InvalidationTrackingMode.WholeChain;
```

| Mode | Description | Memory Impact |
|------|-------------|---------------|
| `None` | No tracking, `InvalidationSource` returns `Unknown` | Lowest |
| `OriginOnly` | Tracks original source only (default) | Low |
| `WholeChain` | Tracks full invalidation chain | Higher |

### Using WholeChain Mode

When `WholeChain` is enabled, you can walk the entire invalidation chain:

```csharp
Invalidation.TrackingMode = InvalidationTrackingMode.WholeChain;

// Later, when debugging:
var computed = await Computed.Capture(() => service.GetData());
// ... some time passes, computed gets invalidated ...

if (computed.IsInvalidated()) {
    // Get the direct source
    var source = computed.InvalidationSource;

    // Get the original root cause
    var origin = source.Origin;
    Console.WriteLine($"Root cause: {origin}");

    // Walk the whole chain
    foreach (var s in source) {
        Console.WriteLine($"  <- {s}");
    }

    // Or format as string
    Console.WriteLine(source.ToString(InvalidationSourceFormat.WholeChain));
    // Output: "GetUser(123) <- GetUserList() <- UserService.cs:42"
}
```

### InvalidationSourceFormat

```csharp
computed.ToString(InvalidationSourceFormat.Default);    // Direct source
computed.ToString(InvalidationSourceFormat.Origin);     // Root cause only
computed.ToString(InvalidationSourceFormat.WholeChain); // Full chain
```

### Custom Invalidation Sources

Provide explicit sources when invalidating:

```csharp
using (Invalidation.Begin()) {
    // Source is auto-captured from caller location
    _ = service.GetData(id);
}

// Or explicit:
computed.Invalidate(new InvalidationSource("Cache expired"));
computed.Invalidate(InvalidationSource.ForCurrentLocation());
```

### Predefined Sources

Fusion uses predefined sources for internal invalidations:

```csharp
InvalidationSource.Unknown        // When tracking is disabled
InvalidationSource.Cancellation   // Cancellation-triggered
InvalidationSource.InitialState   // Initial state invalidation
// ... and others for specific internal operations
```

## Tips

1. **Computed values are immutable** — after `Consistent`, the value never changes; get a new version via `Update()`
2. **Use `Capture` for debugging** — it's the easiest way to inspect the computed graph
3. **Enable `WholeChain` temporarily** — turn it on when debugging invalidation issues, off in production
4. **Don't hold old computed values** — they reference the dependency graph, preventing GC
5. **Prefer `WhenInvalidated` over polling** — it's more efficient and immediate
6. **Use `Changes()` for reactive UIs** — it handles update delays and retries automatically
