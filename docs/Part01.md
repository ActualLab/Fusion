# Core Concepts

Fusion is a library that brings real-time capabilities to your .NET applications with minimal effort. This guide will introduce you to the core concepts and show you how to get started.

Fusion is built around three key abstractions:

1. **Computed Values**, or `Computed<T>` instances, are immutable results of computations that signal when they become outdated (invalidated). Once a `Computed<T>` gets invalidated, you can get its newest version (another instance) by calling its `Update` method.
2. **Compute Services** are services exposing **Computed Methods**. Such methods look very similar to regular methods but produce computed values behind the scenes. You may think of them as "parameterized recipes" for computed values. When you call such a method, it either produces a new `Computed<T>` bound to this specific call (i.e. to the `(service, method, arguments)` triplet) behind the scenes, or pulls the matching one from cache. If the cached value is still consistent (not invalidated yet), the call is resolved without actual computation.
3. Finally, **Computed States** are objects encapsulating a computed value and its auto-update loop. Any computed value knows how to produce the most up-to-date version, but doesn't do this automatically, and that's intentional. For example, if such a value is used in the UI, in some cases you may want to update it instantly (e.g. if we know the user just performed an action, so we want to show its result immediately), but in many other cases it makes sense to throttle down the update rate. Computed states solve exactly this problem: they combine a computed value and its update policy (`IUpdateDelayer`).

## Your First Compute Service

Here's a simple counter service that demonstrates Fusion's basic capabilities:

<!-- snippet: Part01_Declare_Service -->
```cs
public class CounterService : IComputeService // This is a tagging interface any compute service must "implement"
{
    private readonly ConcurrentDictionary<string, int> _counters = new();

    [ComputeMethod] // Indicates this is a compute method
    public virtual async Task<int> Get(string key) // Must be virtual & async
    {
        var value = _counters.GetValueOrDefault(key, 0);
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
        return sum;
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
<!-- endSnippet -->

To use this service, first register it with dependency injection:

<!-- snippet: Part01_Register_Services -->
```cs
var services = new ServiceCollection();
var fusion = services.AddFusion(); // You can also use services.AddFusion(fusion => ...) pattern
fusion.AddComputeService<CounterService>();
var sp = services.BuildServiceProvider();

// And that's how we get our first compute service:
var counters = sp.GetRequiredService<CounterService>();
```
<!-- endSnippet -->

Let's see how the behavior of compute methods in `CounterService` differs from the expected one:

### Automatic Caching

<!-- snippet: Part01_Automatic_Caching -->
```cs
await counters.Get("a"); // Prints: Get(a) = 0
await counters.Get("a"); // Prints nothing -- it's a cache hit; the result is 0
```
<!-- endSnippet -->

Moreover, it works even when compute methods call each other. 
Notice that the `Sum("a", "b")` call here calls `Get("a")`, which gets resolved without an actual computation.
On the other hand, `Get("b")` gets computed. But once we call it again, it also gets resolved from the cache.

<!-- snippet: Part01_Automatic_Dependency_Tracking -->
```cs
await counters.Sum("a", "b"); // Prints: Get(b) = 0, Sum(a, b) = 0 -- Get(b) was called from Sum(a, b)
await counters.Sum("a", "b"); // Prints nothing -- it's a cache hit; the result is 0
await counters.Get("b");      // Prints nothing -- it's a cache hit; the result is 0
```
<!-- endSnippet -->

### Invalidation

Invalidation means marking a certain computed value as "outdated". 
If you have a `Computed<T>` instance, you can do this directly.
But if it's about invalidating a value that corresponds to a certain compute method call,
you can also do this by making this call inside `using (Invalidation.Begin()) { ... }` block:

```csharp
using (Invalidation.Begin())  {
    // Any call to a compute method here:
    // - Won't execute the body of the compute method
    // - Will complete synchronously by returning a completed (Value)Task<T> with Result = default(T)
    // - Will invalidate the cached Computed<T> instance (if it exists) corresponding to the call
}
```

And if you look at the code of the `CounterService.Increment` method, that's exactly what happens
there to invalidate the `Get(key)` call on every increment.

<!-- snippet: Part01_Invalidation -->
```cs
counters.Increment("a"); // Prints: Increment(a) + invalidates Get(a) call result
await counters.Get("a"); // Prints: Get(a) = 1
await counters.Get("b"); // Prints nothing -- Get(b) call wasn't invalidated, so it's a cache hit
```
<!-- endSnippet -->

### Automatic Dependency Tracking and Cascading Invalidation

We know that when a compute method gets called, it builds or uses an existing `Computed<T>` instance,
which stores the cached call result and tracks its invalidation. But there is one other thing `Computed<T>` does: it tracks dependencies of a computation that produced this computed value.

Each `Computed<T>` instance knows all other `Computed<T>` instances that were "used" to produce it,
and vice versa - each computed value can enumerate every other computed value that depends on it, directly or indirectly.

Mathematically speaking, computed values form a Directed Acyclic Graph (DAG) of dependencies between each other. And this graph evolves at runtime:
- When a compute method gets called, it produces a new node (`Computed<T>` instance) in this graph. The edges of this node point to every other node it "uses"; they're added when the compute method runs - specifically, when it calls other compute methods.
- When `Computed<T>` gets invalidated, all of its dependencies (i.e. nodes that use it directly or indirectly) get invalidated as well. Any invalidated node is implicitly removed from the graph, because there can be no edges pointing to it.

Let's see all of this in action:

<!-- snippet: Part01_Cascading_Invalidation -->
```cs
counters.Increment("a"); // Prints: Increment(a)

// Increment(a) invalidated Get(a), but since invalidations are cascading,
// and Sum(a, b) depends on Get(a), it's also invalidated.
// That's why Sum(a, b) is going to be recomputed on the next call, as well as Get(a),
// which is called by Sum(a, b).
await counters.Sum("a", "b"); // Prints: Get(a) = 2, Sum(a, b) = 2
await counters.Sum("a", "b"); // Prints nothing - it's a cache hit; the result is 0

// Even though we expect Sum(a, b) == Sum(b, a), Fusion doesn't know that.
// Remember, "cache key" for any compute method call is (service, method, args...),
// and arguments are different in this case: (a, b) != (b, a).
// So Fusion will have to compute Sum(b, a) from scratch.
// But note that Get(a) and Get(b) calls it makes are still resolved from cache.
await counters.Sum("b", "a"); // Prints: Sum(b, a) = 2 -- Get(b) and Get(a) results are already cached
```
<!-- endSnippet -->

## Computed values

You already know that compute methods produce computed values (`Computed<T>` instances) 
behind the scenes.

Let's pull a `Computed<T>` instance that is associated with a given call and play with it:

<!-- snippet: Part01_Accessing_Computed_Values -->
```cs
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
<!-- endSnippet -->

## Reactive Updates on Invalidation

Now we are ready to write a basic reactive update loop:

<!-- snippet: Part01_Reactive_Updates -->
```cs
_ = Task.Run(async () => {
    // This is going to be our update loop
    for (var i = 0; i <= 5; i++) {
        await Task.Delay(1000);
        counters.Increment("a");
    }
});

var stopwatch = Stopwatch.StartNew();
var computed = await Computed.Capture(() => counters.Sum("a", "b"));
WriteLine($"{stopwatch.Elapsed.TotalSeconds:F1}s: {computed}, Value = {computed.Value}");
for (var i = 0; i < 5; i++) {
    await computed.WhenInvalidated();
    computed = await computed.Update();
    WriteLine($"{stopwatch.Elapsed.TotalSeconds:F1}s: {computed}, Value = {computed.Value}");
}
```
<!-- endSnippet -->

#### [Next: Part 02 &raquo;](./Part02.md) | [Documentation Home](./README.md) 
