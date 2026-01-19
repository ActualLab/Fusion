# Core Concepts Cheat Sheet

Quick reference for Fusion core concepts.

## Compute Services

Compute service interface:

```cs
public interface ICartService : IComputeService
{
    [ComputeMethod]
    Task<List<Order>> GetOrders(long cartId, CancellationToken cancellationToken = default);
}
```

Compute service implementation:

```cs
public class CartService : ICartService
{
    // Must be virtual + return Task<T>
    public virtual async Task<List<Order>> GetOrders(long cartId, CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

Register compute service:

```cs
var fusion = services.AddFusion();
fusion.AddService<ICartService, CartService>();
```

Add invalidation logic:

```cs
using (Invalidation.Begin()) {
    _ = GetOrders(cartId, default);
}
```

## Working with `Computed<T>`

Capture:

```cs
var computed = await Computed.Capture(() => service.GetData(id, cancellationToken));
```

Check consistency:

```cs
if (computed.IsConsistent()) { /* ... */ }
```

Await invalidation:

```cs
await computed.WhenInvalidated(cancellationToken);
// Or
computed.Invalidated += c => Console.WriteLine("Invalidated!");
```

Get current computed inside a compute method:

```cs
var computed = Computed.GetCurrent();
```

Update (recompute):

```cs
var newComputed = await computed.Update(cancellationToken);
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

## States

Create mutable state:

```cs
var stateFactory = services.StateFactory();
var state = stateFactory.NewMutable<int>(initialValue: 0);

state.Set(42);           // Set value
var value = state.Value; // Read value
var value = await state.Use(cancellationToken); // Use in compute methods
```

Create computed state:

```cs
using var computedState = stateFactory.NewComputed(
    new ComputedState<string>.Options() {
        InitialValue = "",
        UpdateDelayer = FixedDelayer.Get(1), // 1 second delay
    },
    async (state, cancellationToken) => {
        var data = await service.GetData(cancellationToken);
        return data.ToString();
    });

await computedState.Update(); // Wait for first computation
var value = computedState.Value;
```
