# Compute Services: Diagrams

Text-based diagrams for the core concepts introduced in [Part 01](Part01.md).

## Core Abstractions Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Fusion Core Abstractions                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌─────────────────┐    produces    ┌─────────────────┐                    │
│   │ Compute Method  │ ─────────────► │   Computed<T>   │                    │
│   │ [ComputeMethod] │                │  (cached value) │                    │
│   └─────────────────┘                └─────────────────┘                    │
│           │                                   │                             │
│           │ defined in                        │ tracked by                  │
│           ▼                                   ▼                             │
│   ┌──────────────────┐               ┌─────────────────┐                    │
│   │ Compute Service  │               │    State<T>     │                    │
│   │ :IComputeService │               │  (auto-update)  │                    │
│   └──────────────────┘               └─────────────────┘                    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Compute Service Registration Flow

```
    var services = new ServiceCollection();
              │
              ▼
    ┌─────────────────────────────────┐
    │  services.AddFusion()           │ ──► Returns FusionBuilder
    └────────────────┬────────────────┘
                     │
                     ▼
    ┌─────────────────────────────────┐
    │  fusion.AddComputeService       │ ──► Registers service with
    │        <CounterService>()       │     proxy generation for
    └────────────────┬────────────────┘     [ComputeMethod] interception
                     │
                     ▼
    ┌─────────────────────────────────┐
    │  services.BuildServiceProvider()│ ──► IServiceProvider
    └────────────────┬────────────────┘
                     │
                     ▼
    ┌─────────────────────────────────┐
    │  sp.GetRequiredService          │ ──► Returns proxied service
    │    <CounterService>()           │     with caching + tracking
    └─────────────────────────────────┘
```

## `Computed<T>` Lifecycle States

```
                        ┌──────────────────┐
                        │    Computing     │
                        │    (mutable)     │
                        └────────┬─────────┘
                                 │
                                 │ computation completes
                                 ▼
                        ┌──────────────────┐
                        │    Consistent    │◄────────────────┐
                        │   (immutable)    │                 │
                        └────────┬─────────┘                 │
                                 │                           │
                                 │ Invalidate()              │ Update()
                                 ▼                           │
                        ┌──────────────────┐                 │
                        │   Inconsistent   │─────────────────┘
                        │   (immutable)    │   creates new Computed<T>
                        └──────────────────┘
```

## Capturing `Computed<T>` Values

```
    // Direct call - just returns the value
    await counters.Get("a");  ──────────►  int (value only)


    // Computed.Capture - returns the Computed<T> wrapper
    await Computed.Capture(() => counters.Get("a"));

                     │
                     ▼
            ┌─────────────────────────────────────────┐
            │            Computed<int>                │
            ├─────────────────────────────────────────┤
            │  .Value           → int                 │
            │  .IsConsistent()  → bool                │
            │  .Invalidate()    → void                │
            │  .Update()        → Task<Computed<T>>   │
            │  .WhenInvalidated()→ Task               │
            │  .When(predicate) → Task<Computed<T>>   │
            │  .Changes()       → IAsyncEnumerable    │
            │  .Invalidated     → event               │
            └─────────────────────────────────────────┘
```

## Invalidation Block Behavior

```
    using (Invalidation.Begin()) {
        _ = Get(key);  // Does NOT execute method body
    }

    ┌──────────────────────────────────────────────────────────────┐
    │                  Inside Invalidation Block                   │
    ├──────────────────────────────────────────────────────────────┤
    │  Compute method call behavior:                               │
    │  ┌─────────────────────────────────────────────────────────┐ │
    │  │ 1. Does NOT execute method body                         │ │
    │  │ 2. Returns completed Task<T> with default(T)            │ │
    │  │ 3. Invalidates cached Computed<T> for this call         │ │
    │  └─────────────────────────────────────────────────────────┘ │
    └──────────────────────────────────────────────────────────────┘
```

## Computed Value Dependency Graph (DAG)

Example from Part01: `Sum("a", "b")` depends on `Get("a")` and `Get("b")`.

```
                    ┌─────────────────────────┐
                    │     Sum("a", "b")       │
                    │   Computed<int> = 2     │
                    └───────────┬─────────────┘
                                │
                    depends on  │
                ┌───────────────┴───────────────┐
                │                               │
                ▼                               ▼
    ┌───────────────────────┐       ┌───────────────────────┐
    │        Get("a")       │       │       Get("b")        │
    │   Computed<int> = 2   │       │   Computed<int> = 0   │
    └───────────────────────┘       └───────────────────────┘
```

### Cascading Invalidation Flow

```
    ┌─────────────────────────────────────────────────┐
    │  counters.Increment("a");                       │
    │  // Inside Increment():                         │
    │  //   _counters.AddOrUpdate(key, ...);          │
    │  //   using (Invalidation.Begin()) {            │
    │  //       _ = Get(key); // invalidates Get("a") │
    │  //   }                                         │
    └─────────────────────────────────────────────────┘
          │
          │ invalidates
          ▼
    ┌───────────────────────┐
    │      Get("a")         │ ──► Inconsistent
    └───────────────────────┘
                │
                │ cascades to dependents
                ▼
    ┌───────────────────────┐
    │     Sum("a", "b")     │ ──► Inconsistent
    └───────────────────────┘
```

## Compute Method Cache Resolution

```
                     Call: counters.Get("a")
                              │
                              ▼
                ┌─────────────────────────────┐
                │  Lookup cache key:          │
                │  (service, method, args)    │
                │  = (counters, Get, "a")     │
                └──────────────┬──────────────┘
                               │
           ┌───────────────────┴───────────────────┐
           │                                       │
     Cache Hit                               Cache Miss
     (Consistent)                            (or Inconsistent)
           │                                       │
           ▼                                       ▼
    ┌─────────────┐                      ┌─────────────────────┐
    │   Return    │                      │  Execute method     │
    │   cached    │                      │  body, create new   │
    │   value     │                      │  Computed<T>        │
    └─────────────┘                      └─────────────────────┘
```

## `State<T>` Inheritance Hierarchy

```
                              IState
                                │
                                │ implements
                                ▼
                           IState<T>
                                │
                                │ implements
                                ▼
                            State<T>
                                │
            ┌───────────────────┴───────────────────┐
            │                                       │
            ▼                                       ▼
    ┌───────────────────┐               ┌───────────────────┐
    │  MutableState<T>  │               │ ComputedState<T>  │
    ├───────────────────┤               ├───────────────────┤
    │ • Manual value    │               │ • Auto-computed   │
    │   assignment      │               │   via lambda      │
    │ • Set(value)      │               │ • Auto-update     │
    │ • Always          │               │   on invalidation │
    │   consistent      │               │ • IUpdateDelayer  │
    └───────────────────┘               │ • Must dispose    │
                                        └───────────────────┘
```

## `ComputedState<T>` Update Loop

```
    ┌─────────────────────────────────────────────────────────────────────┐
    │                    ComputedState<T> Update Cycle                     │
    └─────────────────────────────────────────────────────────────────────┘

         ┌──────────┐
         │  Start   │
         └────┬─────┘
              │
              ▼
    ┌─────────────────────┐
    │   Compute value     │◄──────────────────────────────────────┐
    │   (run lambda)      │                                       │
    └──────────┬──────────┘                                       │
               │                                                  │
               ▼                                                  │
    ┌─────────────────────┐                                       │
    │   State: Consistent │                                       │
    │   Fire: Updated     │                                       │
    └──────────┬──────────┘                                       │
               │                                                  │
               │ dependency invalidated                           │
               ▼                                                  │
    ┌─────────────────────┐                                       │
    │  State: Invalidated │                                       │
    │  Fire: Invalidated  │                                       │
    └──────────┬──────────┘                                       │
               │                                                  │
               │ IUpdateDelayer.Delay()                           │
               ▼                                                  │
    ┌─────────────────────┐                                       │
    │   Fire: Updating    │───────────────────────────────────────┘
    └─────────────────────┘
```

## `MutableState<T>` vs `ComputedState<T>`

```
┌────────────────────────────────────┬────────────────────────────────────┐
│         MutableState<T>            │        ComputedState<T>            │
├────────────────────────────────────┼────────────────────────────────────┤
│                                    │                                    │
│   User Code                        │   User Code                        │
│       │                            │       │                            │
│       │ Set(value)                 │       │ (dependency invalidated)   │
│       ▼                            │       ▼                            │
│  ┌──────────┐                      │  ┌──────────┐                      │
│  │  State   │ ──► New Computed<T>  │  │  State   │ ──► Invalidated      │
│  └──────────┘     (always          │  └────┬─────┘                      │
│                    consistent)     │       │                            │
│                                    │       │ after delay                │
│                                    │       ▼                            │
│                                    │  ┌──────────────┐                  │
│                                    │  │ Recompute    │ ──► New          │
│                                    │  │ (run lambda) │     Computed<T>  │
│                                    │  └──────────────┘                  │
│                                    │                                    │
├────────────────────────────────────┼────────────────────────────────────┤
│ Use case: UI input state           │ Use case: Derived/reactive values  │
│ (e.g., search box text)            │ (e.g., filtered list, totals)      │
└────────────────────────────────────┴────────────────────────────────────┘
```
