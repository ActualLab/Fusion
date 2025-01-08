# Part 2: Computed Values and Computed&lt;T&gt;

Video covering this part:

[<img src="./img/Part2-Screenshot.jpg" width="200"/>](https://youtu.be/DogHOuyRX20)

Fusion's `Computed<T>` is a fairly unique abstraction describing
the result of a computation. Here is how it compares with computed
observables from Knockout.js and MobX:

![](./img/IComputed-Features.jpg)

We're going to use the same `CounterService` and `CreateServices` helper
as in Part 1:

snippet: Part02_CounterService

First, let's try to "pull" `Computed<T>` instance created behind the
scenes for a given call:

snippet: Part02_CaptureComputed

The output:

```text
Get(a)
Computed: Computed`1(Intercepted:CounterService.Get(a) @xIs0saqEU, State: Consistent)
- IsConsistent(): True
- Value:          0
```

As you may notice, `Computed<T>` stores:

- Some representation of its input: `Intercepted:CounterService.Get(a)`
- Version: `@xIs0saqEU`
- State: `Consistent`
- Value: `0`

Overall, its key properties include:

* `ConsistencyState`, which transitions from
  `Computing` to `Computed` and `Invalidated` over its lifetime.
  `IsConsistent()` extension method is a shortcut checking whether the state
  is exactly `Consistent`.
  You may find more of such shortcuts by Ctrl-clicking on `IsConsistent()`.
* `Version` property — an unique value for any `Computed<T>` instance.
  `LTag` struct uses 64-bit integer under the hood, so "unique" actually means
  "unique assuming you don't run a process for a few hundreed years".
* `Output`, `Value` and `Error` — the properties describing the
  result of the computation.
* `Invalidated` — an event raised on invalidation. Handlers of this event
  should never throw exceptions.

`Computed<T>` implements a set of interfaces — most notably,

* `IResult<T>` — interestingly, it both "mimics" `IResult<T>` behavior,
  but also exposes a property of `Result<T>` type.
  * `IResult<T>` describes an object that stores the result of computation
    of type `T`, which is either a `Value` of `T`, or an `Error`
    (of `Exception` type). The interface itself provides a number of
    convenience methods (such as `IsValue(out var value)`, etc.),
    and there are a few extension methods for it as well.
  * `Result<T>` is its struct-based implementation that's frequently used
    to store the actual result. `Computed<T>.Output` is the property
    of exactly this type. But since `Computed<T>` implements `IResult<T>`
    as well (by, basically, forwarding all the calls to its `Output` property),
    you can write `computed.Value` instead of `computed.Output.Value` and so on.
  * Later you'll find out there are other types in Fusion that follow
    the same pattern (i.e. similarly implement `IResult<T>`) — in particular,
    `IState<T>`.
* `IComputedImpl` — an interface  allowing computed instances
  to list themselves as dependencies of other computed instances.
  Most likely you won't ever need to use it, which is why the interface
  is declared in `ActualLab.Fusion.Internal` namespace and implemented explicitly.
  It's mentioned here mostly to explain that dependency graph in Fusion
  is explicit, and this interface provides a way to update it. Most of
  other frameworks rely on event handlers to implement cascading invalidations,
  which actually is quite inefficient from GC perspective: it's enough
  to keep reference to *either* dependency or dependent instance to ensure
  *both* stay in RAM. Fusion, on contrary, doesn't prevent unreferenced
  dependent instances to be garbage collected.
* `IComputed`, `IResult` — "untyped" versions of `Computed<T>` and
  `IResult<T>`. Similarly to `IEnumerable` (vs `IEnumerable<T>`), you can
  use them when the type of result isn't known.

And finally, there are a few important methods:

* `Invalidate()` — triggers invalidation, which turns the instance
  into `Invalidated` state. This is the only change that may happen
  with `Computed<T>` over its lifetime; other than that, computed
  instances are immutable.
  As with `IDisposable.Dispose`, you are free to call this method
  multiple times, though only the first call matters.
* `WhenInvalidated(...)` — an extension method allowing to await
  for invalidation.
* `Update(...)` — *finds or computes* the consistent version
  of this computed instance. *Finds* means the computation will happen
  if and only if there is no cached consistent instance in
  `ComputedRegistry` (~ a cache tracking the most up-to-date version of
  every computed while they're used).
  Note that it returns the most up-to-date computed, which is *likely,
  but not necessarily, consistent*, because it could be invalidated
  either in between the computation and the moment you check its status,
  or even right during the computation.
* `Use(...)` — gets the most up-to-date value of the current computed and
  makes sure that if this happens inside the computation of another
  computed value, the current `Computed<T>` gets listed as a dependency
  of this "outer"  computed.

A bit of code to help remembering this:

```cs
interface IResult<T> {
  T ValueOrDefault { get; } // Never throws an error
  T Value { get; } // Throws Error when HasError
  Exception? Error { get; }
  bool HasValue { get; } // Error == null
  bool HasError { get; } // Error != null

  void Deconstruct(out T value, out Exception? error);
  bool IsValue(out T value);
  bool IsValue(out T value, out Exception error);
  
  Result<T> AsResult();  // Result<T> is a struct implementing IResult<T>
  Result<TOther> Cast<TOther>();
}

// CancellationToken argument is removed everywhere for simplicity
interface Computed<T> : IResult<T> {
  ConsistencyState ConsistencyState { get; } 
  
  event Action Invalidated; // Event, triggered on the invalidation
  Task WhenInvalidated(); // Async way to await for the invalidation
  void Invalidate();
  Task<Computed<T>> Update(); // Notice it returns a new instance!
  Task<T> Use();
}
```

A diagram showing how `ConsistencyState` transition works:

![](./diagrams/consistency-state/transitions.dio.svg)

Since every `Computed<T>` is *almost immutable*, a new instance of
`IComputed` gets created on recomputation if the most recent one is
already invalidated at this point (otherwise there is no reason to
recompute). Here is how 3 computations (of the same value) look
on a Gantt chart:

![](./diagrams/consistency-state/instances.dio.svg)

An ugly visualization showing how multiple `Computed<T>`
instances get invalidated and eventually replaced with their consistent
versions:

![](./img/Invalidate-Update.gif)

> Earlier `Computed<T>.Update(...)` was called `Renew`,
> so as you might guess, the animation was made before this rename :)

Ok, let's get back to code and see how invalidation *really* works:

snippet: Part02_InvalidateComputed1

The output:

```text
Get(a)
computed: Computed`1(Intercepted:CounterService.Get(a) @1EhL08uaNN, State: Consistent)
computed.Invalidate()
computed: Computed`1(Intercepted:CounterService.Get(a) @1EhL08uaNN, State: Invalidated)
Get(a)
newComputed: Computed`1(Intercepted:CounterService.Get(a) @1EhL08uaPR, State: Consistent)
```

Compare the above code with this one:

snippet: Part02_InvalidateComputed2

The output:

```text
Get(a)
computed: Computed`1(Intercepted:CounterService.Get(a) @R0oNKnVbo, State: Consistent)
using Invalidation.Begin(() => counters.Get("a"))
computed: Computed`1(Intercepted:CounterService.Get(a) @R0oNKnVbo, State: Invalidated)
Get(a)
newComputed: Computed`1(Intercepted:CounterService.Get(a) @R0oNKnVds, State: Consistent)
```

The output is ~ identical. As you might guess,

* `Invalidation.Begin(...)` uses `Computed.Capture` under the hood
  to capture the computed instance and invalidate it.
* `IComputed.Update(...)` invokes the method that was used to
  produce it and similarly captures the newly produced computed.

And finally, let's see how you can "observe" the invalidation to
trigger the update:

snippet: Part02_IncrementCounter

The output:

```text
Get(a)
9/1/2020 5:08:54 PM: 0
Increment(a)
Get(a)
9/1/2020 5:08:55 PM: 1
Increment(a)
Get(a)
9/1/2020 5:08:56 PM: 2
Increment(a)
Get(a)
9/1/2020 5:08:57 PM: 3
Increment(a)
Get(a)
9/1/2020 5:08:58 PM: 4
Increment(a)
Get(a)
9/1/2020 5:08:59 PM: 5
```

So even though Fusion doesn't update anything automatically,
achieving exactly the same behavior with it is pretty straightforward.

A good question to ask is: but why it doesn't, if literally everyone else
does? E.g.
[MobX even tries to re-compute all the derivations atomically](https://mobx.js.org/intro/concepts.html) — so why Fusion does literally the opposite?

The answer is:

* If your model is tiny (KO / MobX case), these computations are relatively cheap,
  and moreover, they run on the client. So normally it's fine to run them
  even if you aren't going to use the result.
* On contrary, Fusion is designed to deal with huge models "covering" your
  whole data; and even though it works on the client too, the most significant
  work it typically does happens on server, where most of its Compute Services
  live. Recomputing every dependency on change isn't just inefficient, but
  almost never possible in this scenario.

What's totally possible though is to notify the code using certain computed value
that it became obsolete and thus has to be recomputed. And this notification —
plus maybe some other "knowns" about the domain would let the code to
determine the right strategy for triggering the recomputation.

Think of these two cases:

- We're recomputing the "view" of a Twitter post (tweet)
- And in one case we know the current user just pressed "Like" icon there,
  so most likely it was invalidated due to this action. In this case
  we can immediately update it to reflect the result of user action;
  there are no potential scalability problems here.
- In another case the post was invalidated w/o current user action.
  And in this case we may delay the update for several seconds — in fact,
  as for long as we think is reasonable taking into account such factors
  as current backend load or the current rate of actions on this post.

Such an approach allows you to have nearly instant updates in most cases,
but selectively (i.e. per piece of content + user) throttle the update
rate (without affecting user-induced updates) in cases when instant updates
create performance problems.

It worth mentioning that Fusion offers all the abstractions you need to
have this behavior, and moreover, similarly to almost-invisible `Computed<T>`,
you normally don't even need to know these abstractions exist.
But they'll be ready to help you once you conclude you need throttling.

#### [Next: Part 3 &raquo;](./Part03.md) | [Tutorial Home](./README.md)
