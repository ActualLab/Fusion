# Compute Services: Cheat Sheet

Quick reference for Fusion core concepts.


## Configuration

Register Fusion and compute services:

```cs
var fusion = services.AddFusion();
fusion.AddService<ICartService, CartService>();
// or: fusion.AddComputeService<CartService>();
```

Change global computed options defaults:

```cs
ComputedOptions.Default = ComputedOptions.Default with {
    MinCacheDuration = TimeSpan.FromSeconds(30),
};
```

Configure invalidation source tracking (for debugging):

```cs
Invalidation.TrackingMode = InvalidationTrackingMode.WholeChain;
```


## Compute Service Interface

```cs
public interface ICartService : IComputeService
{
    [ComputeMethod]
    Task<List<Order>> GetOrders(long cartId, CancellationToken cancellationToken = default);
}
```


## Compute Service Implementation

```cs
public class CartService : ICartService
{
    // Must be virtual + return Task<T>
    [ComputeMethod]
    public virtual async Task<List<Order>> GetOrders(long cartId, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```


## ComputeMethod Attribute Options

```cs
[ComputeMethod(
    MinCacheDuration = 60,              // Keep in memory for 60 seconds
    AutoInvalidationDelay = 300,        // Auto-refresh every 5 minutes
    TransientErrorInvalidationDelay = 5, // Retry errors after 5 seconds
    InvalidationDelay = 0.5,            // Debounce invalidations by 500ms
    ConsolidationDelay = 0)]            // Invalidate only when value changes
public virtual async Task<Data> GetData() { ... }
```


## Invalidation

Invalidate from a regular method:

```cs
using (Invalidation.Begin()) {
    _ = GetOrders(cartId, default);
}
```

Invalidate a `Computed<T>` directly:

```cs
computed.Invalidate();
computed.Invalidate(TimeSpan.FromSeconds(30));  // Delayed
computed.Invalidate(new InvalidationSource("reason"));  // With source
```


## Working with Computed&lt;T&gt;

Capture:

```cs
var computed = await Computed.Capture(() => service.GetData(id, cancellationToken));
var computed = await Computed.TryCapture(() => service.GetData(id));  // Returns null on failure
```

Get existing (without triggering computation):

```cs
var existing = Computed.GetExisting(() => service.GetData(id));
```

Get current computed inside a compute method:

```cs
var computed = Computed.GetCurrent();
var computed = Computed.GetCurrent<Data>();  // Typed, throws if null
```

Check consistency:

```cs
if (computed.IsConsistent()) { /* ... */ }
if (computed.IsInvalidated()) { /* ... */ }
```

Update (recompute):

```cs
var newComputed = await computed.Update(cancellationToken);
```

Use as dependency:

```cs
T value = await computed.Use(cancellationToken);
T value = await computed.Use(allowInconsistent: true, cancellationToken);  // Allow stale
```

Await invalidation:

```cs
await computed.WhenInvalidated(cancellationToken);
computed.Invalidated += c => Console.WriteLine("Invalidated!");
```

Await until condition is met:

```cs
var computed = await Computed
    .Capture(() => service.GetCount(id, cancellationToken))
    .When(count => count >= 10, cancellationToken);
```

Observe changes:

```cs
var computed = await Computed.Capture(() => service.GetValue(id, cancellationToken));
await foreach (var c in computed.Changes(cancellationToken)) {
    Console.WriteLine($"New value: {c.Value}");
}
```

Deconstruct for pattern matching:

```cs
var (value, error) = computed;
var (value, error, version) = computed;
```

Isolate from dependency tracking:

```cs
using (Computed.BeginIsolation()) {
    // Calls here won't register as dependencies
}
```


## ComputedRegistry

```cs
ComputedRegistry.InvalidateEverything();  // Useful for tests
await ComputedRegistry.Prune();           // Force prune dead entries
```


## States

Get StateFactory:

```cs
var stateFactory = services.StateFactory();
// or: StateFactory.Default (for tests)
```

Create mutable state:

```cs
var state = stateFactory.NewMutable<int>(initialValue: 0);

state.Set(42);           // Set value
state.Value = 42;        // Same as above
var value = state.Value; // Read value
var value = await state.Use(cancellationToken); // Use in compute methods
```

Create computed state:

```cs
using var computedState = stateFactory.NewComputed(
    new ComputedState<string>.Options() {
        InitialValue = "",
        UpdateDelayer = FixedDelayer.Get(1), // 1 second delay
        EventConfigurator = state => {
            state.Updated += (s, _) => Console.WriteLine($"Updated: {s.Value}");
        },
    },
    async (state, cancellationToken) => {
        var data = await service.GetData(cancellationToken);
        return data.ToString();
    });

await computedState.Update(); // Wait for first computation
var value = computedState.Value;
```

State properties:

```cs
var computed = state.Computed;           // Current Computed<T>
var snapshot = state.Snapshot;           // Immutable snapshot
var lastGood = state.LastNonErrorValue;  // Last value before error
```


## Update Delayers

```cs
FixedDelayer.Get(1)        // 1 second delay
FixedDelayer.Get(0.5)      // 500ms delay
FixedDelayer.NextTick      // ~16ms delay
FixedDelayer.MinDelay      // Minimum safe delay (32ms)
```


## State Events

```cs
state.Invalidated += (s, kind) => { /* ... */ };
state.Updating += (s, kind) => { /* ... */ };
state.Updated += (s, kind) => { /* ... */ };
```
