# Core Concepts

Fusion is a library that brings real-time capabilities to your .NET applications with minimal effort. This guide will introduce you to the core concepts and show you how to get started.

Fusion is built around three key abstractions:

1. **Computed Values**, or `Computed<T>` instances, are immutable results of computations that signal when they become outdated (invalidated). Once a `Computed<T>` gets invalidated, you can get its newest version (another instance) by calling its `Update` method.
2. **Compute Services** are services exposing **Computed Methods**. Such methods look very similar to regular methods but produce computed values behind the scenes. You may think of them as "parameterized recipes" for computed values. When you call such a method, it either produces a new `Computed<T>` bound to this specific call (i.e. to the `(service, method, arguments)` triplet) behind the scenes, or pulls the matching one from cache. If the cached value is still consistent (not invalidated yet), the call is resolved without actual computation.
3. Finally, **Computed States** are objects encapsulating a computed value and its auto-update loop. Any computed value knows how to produce the most up-to-date version, but doesn't do this automatically, and that's intentional. For example, if such a value is used in the UI, in some cases you may want to update it instantly (e.g. if we know the user just performed an action, so we want to show its result immediately), but in many other cases it makes sense to throttle down the update rate. Computed states solve exactly this problem: they combine a computed value and its update policy (`IUpdateDelayer`).

## Your First Compute Service

Here's a simple counter service that demonstrates Fusion's basic capabilities:

```csharp
public class CounterService : IComputeService // This is a tagging interface any compute service must "implement"
{
    private readonly ConcurrentDictionary<string, int> _counters = new();

    [ComputeMethod] // Indicates this is a compute method
    public virtual async Task<int> Get(string key) // Must be virtual & async
    {
        var value = _counters.TryGetValue(key, out var value) ? value : 0;
        WriteLine($"Get({key}) = {value}");
        return value;
    }

    [ComputeMethod] // Indicates this is a compute method
    public virtual async Task<int> Sum(string key1, string key2) // Must be virtual & async
    {
        var value1 = await Get(key1);
        var value2 = await Get(key2);
        var sum = value1 + value2;
        WriteLine($"Sum({key1}, {key2}) = {sum}");
        return value;
    }

    // This is a regular method, so there are no special requirements
    public void Increment(string key)
    {
        WriteLine($"Increment({key})");
        _counters.AddOrUpdate(key, k => 1, (k, v) => v + 1);
        using (Invalidation.Begin())  {
            // Any call to a compute method inside this block means "invalidate the value for that call"
            _ = Get(key); // So here we invalidate the value of this.Get(...) call with the `key` argument
        }
    }
}
```

To use this service, first register it with dependency injection:

```csharp
var services = new ServiceCollection();
var fusion = services.AddFusion();
// You can also use services.AddFusion(fusion => ...) pattern
fusion.AddComputeService<CounterService>();
var sp = services.BuildServiceProvider();
```

Let's look at how `CounterService` behavior differs from what you'd expect:

### 1. Automatic Caching
```csharp
var counters = sp.GetRequiredService<CounterService>();
await counters.Get("a"); // Prints: Get(a) = 0
await counters.Get("a"); // Prints nothing - it's a cache hit; the result is 0
await counters.Get("b"); // Prints: Get(b) = 0
await counters.Get("b"); // Prints nothing - it's a cache hit; the result is 0
result is 0
```

### 2. Automatic Dependency Tracking
```csharp
await counters.Sum("a", "b"); // Prints: Sum(a, b) = 0
await counters.Sum("a", "b"); // Prints nothing - it's a cache hit; the result is 0
```

### 3. Invalidation
```csharp
counters.Increment("a"); // Prints: Increment(a) + invalidates Get(a) call result
await counters.Get("a"); // Prints: Get(a) = 1
await counters.Get("b"); // Prints nothing - Get(b) call wasn't invalidated, so it's a cache hit, the result is 0
```

### 4. Cascading Invalidation
```csharp
counters.Increment("a"); // Prints: Increment(a) + invalidates Get(a) call again
await counters.Sum("a", "b"); // Prints: Get(a) = 2, Sum(a, b) = 2
await counters.Sum("a", "b"); // Prints nothing - it's a cache hit; the result is 0
```

### 5. Getting `Computed<T>` for a given call:

```csharp
var computedForGetA = await Computed.Capture(() => counters.Get("a"));
WriteLine(computedForGetA.IsConsistent()); // True
WriteLine(computedForGetA.Value);          // 2

var computedForSumAB = await Computed.Capture(() => counters.Sum("a", "b"));
WriteLine(computedForSumAB.IsConsistent()); // True
WriteLine(computedForSumAB.Value);          // 2

// Adding invalidation handler; you can also use WhenInvalidated
computedForSumAB.Invalidated += _ => WriteLine("Sum(a, b) is invalidated");

// Manually invalidate computedForGetA, i.e. the result of counters.Get("a") call
computedForGetA.Invalidate(); // Prints: Sum(a, b) is invalidated
WriteLine(computedForGetA.IsConsistent());  // False
WriteLine(computedForSumAB.IsConsistent()); // False - invalidation is always cascading

// Manually update computedForSumAB
var newComputedForSumAB = await computedForSumAB.Update();
// Prints:
// Get(a) = 2 - we invalidated it, so it was of Sum(a, b)
// Sum(a, b) = 2 - .Update() call above actually triggered this call

WriteLine(newComputedForSumAB.IsConsistent()); // True
WriteLine(newComputedForSumAB.Value); // 2

// Calling .Update() for consistent Computed<T> returns the same instance
WriteLine(computedForSumAB == newComputedForSumAB); // False
WriteLine(newComputedForSumAB == await computedForSumAB.Update()); // True

// Since `Computed<T>` are almost immutable, 
// the outdated computed instance is still usable:
WriteLine(computedForSumAB.IsConsistent()); // False
WriteLine(computedForSumAB.Value); // 2
```

## What's Next?

- Learn about real-time UI updates with Fusion
- Explore distributed caching capabilities
- Understand dependency tracking in depth
- Work with Fusion in Blazor applications

## Sample Projects

Check out our samples to see Fusion in action:
- [Caching Sample](https://github.com/ActualLab/Fusion.Samples/tree/master/src/Caching)
- [Blazor Sample](https://github.com/ActualLab/Fusion.Samples/tree/master/src/Blazor)

#### [Next: Fusion 02 &raquo;](./Fusion02.md) | [Tutorial Home](./README.md) 