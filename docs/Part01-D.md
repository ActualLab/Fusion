# Compute Services: Diagrams

Text-based diagrams for the core concepts introduced in [Part 01](Part01.md).

## `Computed<T>` Lifecycle States

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Computing
    Computing --> Consistent : completes
    Consistent --> Inconsistent : Invalidate
```

## Capturing `Computed<T>` Values

| Call | Returns |
|------|---------|
| `await counters.Get('a')` | `int` (value only) |
| `await Computed.Capture(() => counters.Get('a'))` | `Computed<int>` |

**`Computed<T>` API:**
| Property/Method | Returns |
|-----------------|---------|
| `.Value` | `int` |
| `.IsConsistent()` | `bool` |
| `.Invalidate()` | `void` |
| `.Update()` | `Task<Computed<T>>` |
| `.WhenInvalidated()` | `Task` |
| `.When(predicate)` | `Task<Computed<T>>` |
| `.Changes()` | `IAsyncEnumerable` |
| `.Invalidated` | event |

## Invalidation Block Behavior

```csharp
using (Invalidation.Begin()) {
    _ = Get(key);  // Does NOT execute method body
}
```

| Behavior | Description |
|----------|-------------|
| Method body | Does NOT execute |
| Return value | `Task.FromResult(default(T))` or `default(T)` |
| Side effect | Invalidates cached `Computed<T>` for this call |

## Computed Value Dependency Graph (DAG)

Example from Part01: `Sum("a", "b")` depends on `Get("a")` and `Get("b")`.

```mermaid
flowchart LR
    GetA["Get('a')<br/>Computed&lt;int&gt; = 2"] -->|used by| Sum["Sum('a', 'b')<br/>Computed&lt;int&gt; = 2"]
    GetB["Get('b')<br/>Computed&lt;int&gt; = 0"] -->|used by| Sum
```

### Cascading Invalidation Flow

```mermaid
flowchart LR
    Inc["Increment('a')"] -->|invalidates| GetA["Get('a')"]
    GetA --> I1(["Inconsistent"])
    GetA -->|cascades| Sum["Sum('a', 'b')"]
    Sum --> I2(["Inconsistent"])
```

## Compute Method Cache Resolution

```mermaid
flowchart LR
    Call["Call: Get('a')"] --> Lookup{"Is cached?"}
    Lookup -->|Yes| Return["Return cached"]
    Lookup -->|No| Lock["Acquire async lock"] --> Check{"Is cached?"}
    Check -->|Yes| Return
    Check -->|No| Exec["Execute & cache"]
```

## `State<T>` Inheritance Hierarchy

```mermaid
classDiagram
    direction LR
    IState_T <|-- State_T
    State_T <|-- MutableState_T
    State_T <|-- ComputedState_T

    class IState_T["IState&lt;T&gt;"] {
        <<interface>>
    }
    class State_T["State&lt;T&gt;"] {
    }
    class MutableState_T["MutableState&lt;T&gt;"] {
        +Set(value)
    }
    class ComputedState_T["ComputedState&lt;T&gt;"] {
        Auto-update on invalidation
    }
```

## `ComputedState<T>` Update Loop

```mermaid
flowchart LR
    Start([Start]) --> Compute["Compute<br/>(run lambda)"]
    Compute --> Consistent["Consistent"]
    Consistent -->|"invalidated"| Invalidated["Invalidated"]
    Invalidated -->|"Delay()"| Updating["Updating"]
    Updating --> Compute
```

## `MutableState<T>` vs `ComputedState<T>`

```mermaid
flowchart LR
    subgraph Mutable["MutableState&lt;T&gt;"]
        direction LR
        MU["User Code"] --> MS["State"] --> MI(["Synchronously invalidated<br/>and recomputed on .Set(..)"]) --> MR["Updated State<br/>New Computed&lt;T&gt;"]
    end

    subgraph Computed["ComputedState&lt;T&gt;"]
        direction LR
        CU["User Code"] --> CS["State"] --> CI(["Invalidated"]) -->|"delay"| CR(["Recompute"]) --> CN["Updated State<br/>New Computed&lt;T&gt;"]
    end
```

| MutableState&lt;T&gt; | ComputedState&lt;T&gt; |
|----------------------|------------------------|
| Use case: UI input state (e.g., search box text) | Use case: Derived/reactive values (e.g., filtered list, totals) |
