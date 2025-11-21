# Part 5: Fusion on Server-Side Only

Even though Fusion supports RPC, you can use it on server-side to cache recurring computations.
Below is the output of
[Caching Sample](https://github.com/ActualLab/Fusion.Samples/tree/master/src/Caching) (slightly outdated):

```text
Local services:
Fusion's Compute Service [-> EF Core -> SQL Server]:
  Reads         : 27.55M operations/s
Regular Service [-> EF Core -> SQL Server]:
  Reads         : 25.05K operations/s

Remote services:
Fusion's Compute Service Client [-> HTTP+WebSocket -> ASP.NET Core -> Compute Service -> EF Core -> SQL Server]:
  Reads         : 20.29M operations/s
RestEase Client [-> HTTP -> ASP.NET Core -> Compute Service -> EF Core -> SQL Server]:
  Reads         : 127.96K operations/s
RestEase Client [-> HTTP -> ASP.NET Core -> Regular Service -> EF Core -> SQL Server]:
  Reads         : 20.46K operations/s
```

Last two results are the most interesting in the context of this part:

- A tiny EF Core-based service exposed via ASP.NET Core controller
  serves **20,500** requests per second. That's already a lot &ndash;
  mostly, because its data set fully fits in RAM on SQL Server.
- An identical service relying on Fusion (it's literally the same code
  plus Fusion's `[ComputeMethod]` and `Invalidation.Begin` calls)
  boosts this number to **128,000** requests per second.

And that's the main reason to use Fusion on server-side only:
5-10x performance boost with a relatively tiny amount of changes.
[Similarly to incremental builds](https://alexyakunin.medium.com/the-ungreen-web-why-our-web-apps-are-terribly-inefficient-28791ed48035?source=friends_link&sk=74fb46086ca13ff4fea387d6245cb52b),
the more complex your logic is, the more you are expected to gain.

## The Fundamentals

You already know that `Computed<T>` instances are reused, but so far
we didn't talk much about the details. Let's learn some specific
aspects of this behavior before jumping to caching.

The service below prints a message once its `Get` method
is actually computed (i.e. its cached value for a given argument isn't reused)
and returns the same value as its input. We'll be using it to
find out when `IComputed` instances are actually reused.

<!-- snippet: Part05_Service1 -->

```cs
public class Service1 : IComputeService
{
    [ComputeMethod]
    public virtual async Task<string> Get(string key)
    {
        WriteLine($"{nameof(Get)}({key})");
        return key;
    }
}

public static IServiceProvider CreateServices()
{
    var services = new ServiceCollection();
    services.AddSingleton<ISwapService, DemoSwapService>();
    services.AddFusion()
        .AddService<Service1>()
        .AddService<Service2>() // We'll use Service2 & other services later
        .AddService<Service3>()
        .AddService<Service4>();
    return services.BuildServiceProvider();
}
```

First, `IComputed` instances aren't "cached" by default &ndash; they're just
reused while it's possible:

<!-- snippet: Part05_Caching1 -->

```cs
var service = CreateServices().GetRequiredService<Service1>();
// var computed = await Computed.Capture(() => counters.Get("a"));
WriteLine(await service.Get("a"));
WriteLine(await service.Get("a"));
GC.Collect();
WriteLine("GC.Collect()");
WriteLine(await service.Get("a"));
WriteLine(await service.Get("a"));
```

The output:

```text
Get(a)
a
a
GC.Collect()
Get(a)
a
a
```

As you see, `GC.Collect()` call removes cached `IComputed`
for `Get("a")` &ndash; and that's why `Get(a)` is printed
twice here.

All of this means that most likely Fusion holds a
[weak reference](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/weak-references)
to this value (in reality it uses `GCHandle`-s for performance reasons, but
technically they do the same).

Let's prove this by uncomment the commented line:

<!-- snippet: Part05_Caching2 -->

```cs
var service = CreateServices().GetRequiredService<Service1>();
var computed = await Computed.Capture(() => service.Get("a"));
WriteLine(await service.Get("a"));
WriteLine(await service.Get("a"));
GC.Collect();
WriteLine("GC.Collect()");
WriteLine(await service.Get("a"));
WriteLine(await service.Get("a"));
```

The output:

```text
Get(a)
a
a
GC.Collect()
a
a
```

As you see, assigning a strong reference to `IComputed` is enough
to ensure it won't recompute on the next call.

> So to truly cache some `IComputed`, you need to store a strong
> reference to it and hold it while you want it to be cached.

Now, if you compute `f(x)`, is it enough to store
a computed for its output to ensure its dependencies
are cached too? Let's test this:

<!-- snippet: Part05_Service2 -->

```cs
public class Service2 : IComputeService
{
    [ComputeMethod]
    public virtual async Task<string> Get(string key)
    {
        WriteLine($"{nameof(Get)}({key})");
        return key;
    }

    [ComputeMethod]
    public virtual async Task<string> Combine(string key1, string key2)
    {
        WriteLine($"{nameof(Combine)}({key1}, {key2})");
        return await Get(key1) + await Get(key2);
    }
}
```

<!-- snippet: Part05_Caching3 -->

```cs
var service = CreateServices().GetRequiredService<Service2>();
var computed = await Computed.Capture(() => service.Combine("a", "b"));
WriteLine("computed = Combine(a, b) completed");
WriteLine(await service.Combine("a", "b"));
WriteLine(await service.Get("a"));
WriteLine(await service.Get("b"));
WriteLine(await service.Combine("a", "c"));
GC.Collect();
WriteLine("GC.Collect() completed");
WriteLine(await service.Get("a"));
WriteLine(await service.Get("b"));
WriteLine(await service.Combine("a", "c"));
```

The output:

```text
Combine(a, b)
Get(a)
Get(b)
computed = Combine(a, b) completed
ab
a
b
Combine(a, c)
Get(c)
ac
GC.Collect() completed
a
b
Combine(a, c)
Get(c)
ac
```

As you see, yes,

> Strong referencing an `IComputed` ensures every other `IComputed`
> instance it depends on also stays in memory.

Let's check if the opposite is true as well:

<!-- snippet: Part05_Caching4 -->

```cs
var service = CreateServices().GetRequiredService<Service2>();
var computed = await Computed.Capture(() => service.Get("a"));
WriteLine("computed = Get(a) completed");
WriteLine(await service.Combine("a", "b"));
GC.Collect();
WriteLine("GC.Collect() completed");
WriteLine(await service.Combine("a", "b"));
```

The output:

```text
Get(a)
computed = Get(a) completed
Combine(a, b)
Get(b)
ab
GC.Collect() completed
Combine(a, b)
Get(b)
ab
```

So the opposite is not true.

But why Fusion behaves this way? The answer is actually super simple:

- Fusion has to strong-reference every computed instance used to produce an output
  because if any of them gets garbage collected before the output itself,
  the invalidation passing through such an instance simply won't reach the output.
- But since it has to propagate the invalidation events from dependencies
  (used values) to dependants (values produced from the used ones),
  it also has to reference the direct dependants from every dependency.
  And these references have to be either weak
  or simply shouldn't be .NET object references, coz if they were strong
  references, this would almost certainly keep the whole graph of `IComputed`
  in memory, which is highly undesirable. That's why Fusion uses the second
  option here &ndash; it stores keys of direct dependants of every `IComputed`
  and resolves them via the same registry as it uses to resolve `IComputed`
  instances for method calls.
- Interestingly, there is a fundamental reason to weak reference every
  `IComputed`: imagine Fusion doesn't do this, so calling the same compute
  method twice with the same arguments may produce two different `IComputed`
  instances representing the output (of the same computation). Now imagine
  you invalidate the first one (and thus all of its dependants), but
  don't do anything with the second one (so its dependants stay valid).
  As you see, it's a partial invalidation, i.e. something that may cause
  a long-term inconsistency &ndash; which is why Fusion ensures that at any
  moment of time there can be only one `IComptuted` instance describing
  given computation.

**Default caching behavior &ndash; a summary:**

- Nothing is really cached, but everything is weak-referenced
- When you compute a new value, every value used to produce it
  becomes strong-referenced from the produced one
- So if you strong reference some `IComputed`, you effectively
  hold the whole graph of what it's composed from in memory.
- If `IComputed` containing the output of `f(someArgs)` doesn't
  exist in RAM, it also means none of its possible dependants
  exist in RAM.

## Caching Options

As you probably already understood, Fusion allows you to
implement any desirable caching behavior: all you need
is your own service that will hold a strong reference
to whatever you want to cache for as long as you want.

Besides that, Fusion offers two built-in options:

- `[ComputeMethod(MinCacheDuration = TimeInSeconds)]`,
  ensures a strong reference w/ specified expiration
  time is added to
  [Timeouts.KeepAlive](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion/Internal/Timeouts.cs)
  timer set every time the output is used. In fact, it sets a minimum
  time the output of a function stays in RAM.
- `[Swap(SwapTime = TimeInSeconds)]` enables swapping feature
  for the compute method's output. It's a kind of similar to
  OS page swapping, but stores & loads back the output instead.

Let's look at how they work.

### ComputeMethodAttribute.MinCacheDuration

Let's just add `MinCacheDuration` to the service we were using previously:

<!-- snippet: Part05_Service3 -->

```cs
public class Service3 : IComputeService
{
    [ComputeMethod]
    public virtual async Task<string> Get(string key)
    {
        WriteLine($"{nameof(Get)}({key})");
        return key;
    }

    [ComputeMethod(MinCacheDuration = 0.3)] // MinCacheDuration was added
    public virtual async Task<string> Combine(string key1, string key2)
    {
        WriteLine($"{nameof(Combine)}({key1}, {key2})");
        return await Get(key1) + await Get(key2);
    }
}
```

And run this code:

<!-- snippet: Part05_Caching5 -->

```cs
var service = CreateServices().GetRequiredService<Service3>();
WriteLine(await service.Combine("a", "b"));
WriteLine(await service.Get("a"));
WriteLine(await service.Get("x"));
GC.Collect();
WriteLine("GC.Collect()");
WriteLine(await service.Combine("a", "b"));
WriteLine(await service.Get("a"));
WriteLine(await service.Get("x"));
await Task.Delay(1000);
GC.Collect();
WriteLine("Task.Delay(...) and GC.Collect()");
WriteLine(await service.Combine("a", "b"));
WriteLine(await service.Get("a"));
WriteLine(await service.Get("x"));
```

The output:

```text
Combine(a, b)
Get(a)
Get(b)
ab
a
Get(x)
x
GC.Collect()
ab
a
Get(x)
x
Task.Delay(...) and GC.Collect()
Combine(a, b)
Get(a)
Get(b)
ab
a
Get(x)
x
```

As you see, `MinCacheDuration` does exactly what's expected:

- It holds a strong reference to the output of `Combine` for 0.3 seconds,
  so the output of `Combine("a", "b")` gets cached for 0.3s
- Since `Combine` calls `Get`, the outputs of
  `Get("a")` and `Get("b")` are cached for 0.3s too
- But not the output of `Get("x")`, which wasn't used in any of
  `Combine` calls in this example.

That's basically it on `MinCacheDuration`.

A few tips on how to use it:

- You should apply it mainly to the final outputs &ndash; i.e. compute
  methods that are either exposed via API or used in your UI.
- Applying it to other compute methods is fine too, though keep in mind
  that whatever is used by top level methods with `MinCacheDuration`
  is anyway cached for the same period, so probably you don't need this.
- And in general, keep in mind that ideally you want to "recompose"
  or aggregate the outputs of compute methods rather than "rewrite".
  In other words, if you have a chance to use the same object you get
  from the downstream method &ndash; do this, because this won't incur
  use of extra RAM.
- This is also why you might want to return just immutable objects from
  compute methods &ndash; and C# 9 records come quite handy here.

---

**P.S.** If you love algorithms and data structures, check out
[ConcurrentTimerSet&lt;TTimer&gt;](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Core/Time/ConcurrentTimerSet.cs) &ndash;
Fusion uses its own implementation of timers to ensure they
scale much better than `Task.Delay` (which relies on
[TimerQueue](https://referencesource.microsoft.com/#mscorlib/system/threading/timer.cs,29)),
though this comes at cost of fire precision: Fusion timers fire only
[4 times per second](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Fusion/Internal/Timeouts.cs#L20).
Under the hood, `ConcurrentTimerSet` uses
[RadixHeapSet](https://github.com/ActualLab/Fusion/blob/master/src/ActualLab.Core/Collections/RadixHeapSet.cs) &ndash;
basically, a [Radix Heap](http://ssp.impulsetrain.com/radix-heap.html)
supporting `O(1)` find and delete operations.
