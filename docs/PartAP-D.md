# Interceptors and Proxies: Diagrams

Diagrams for the concepts introduced in [Interceptors and Proxies](PartAP.md).

## Proxy Call Flow

How a method call flows through the proxy system:

```mermaid
flowchart TD
    Client["Client Code<br/>proxy.GreetAsync(#quot;World#quot;)"]
    Client --> Proxy

    subgraph Proxy ["IGreetingServiceProxy"]
        P1["1. Create Invocation(proxy, method, arguments, intercepted)"]
        P2["2. Call interceptor.Intercept&lt;TResult&gt;(invocation)"]
        P1 --> P2
    end

    Proxy --> Interceptor

    subgraph Interceptor ["Interceptor"]
        I1["1. GetHandler(invocation) - lookup or create"]
        I2["2. handler(invocation) - execute your logic"]
        I1 --> I2
    end

    Interceptor -->|"if pass-through proxy"| Target

    subgraph Target ["Target&nbsp;Service"]
        T1["invocation.InvokeIntercepted() -> real implementation"]
    end

    Target --> Result["Client receives result"]
```


## Proxy Generation at Compile Time

```mermaid
flowchart LR
    Interface["Your Interface<br/>IGreetingService<br/>: IRequiresAsyncProxy"]
    Generator["Source Generator<br/>ActualLab.Generators<br/>ProxyGenerator"]
    Proxy["Generated Proxy<br/>IGreetingServiceProxy<br/>: InterfaceProxy, IGreetingService"]

    Interface -->|scans| Generator
    Generator -->|emits| Proxy
```


## Proxy Type Hierarchy

```mermaid
classDiagram
    direction TB
    IRequiresAsyncProxy <|-- IRequiresFullProxy
    InterfaceProxy <|-- IGreetingServiceProxy

    class IRequiresAsyncProxy {
        <<interface>>
    }
    class IRequiresFullProxy {
        <<interface>>
        also intercepts sync methods
    }
    class InterfaceProxy {
        ProxyTarget: object?
    }
    class IGreetingServiceProxy {
        implements IGreetingService, IProxy
        __interceptor: Interceptor
        __cachedIntercepted0: Func
        __cachedIntercept0: Func
        GreetAsync(name, ct)
    }
```

### Generated Proxy Fields

| Field | Type | Purpose |
|-------|------|---------|
| `__interceptor` | `Interceptor` | The interceptor instance |
| `__cachedIntercepted0` | `Func<ArgumentList, Task<string>>` | Cached delegate to target |
| `__cachedIntercept0` | `Func<Invocation, Task<string>>` | Cached intercept delegate |
| `ProxyTarget` | `object?` | Real service (from `InterfaceProxy`) |


## Invocation Structure

| Field | Description |
|-------|-------------|
| `Proxy` | The proxy instance (e.g., `IGreetingServiceProxy`) |
| `Method` | `MethodInfo` of the called method |
| `Arguments` | `ArgumentList` containing method arguments |
| `InterceptedDelegate` | Delegate to call the real implementation (for pass-through) |
| `InterfaceProxyTarget` | The real service instance |


## ArgumentList Variants

```mermaid
flowchart TD
    New["ArgumentList.New&lt;T0, T1&gt;(arg0, arg1)"] --> Check{"UseGenerics?"}

    Check -->|"Yes (1-4 args)"| Generic["ArgumentListG2&lt;T0,T1&gt;<br/>• Generic storage<br/>• No boxing for value types<br/>• Faster access"]
    Check -->|"No (or 5+ args)"| Simple["ArgumentListS2<br/>• object? storage<br/>• Boxing for value types<br/>• All platforms"]
```

| Count | Generic Type | Simple Type |
|-------|--------------|-------------|
| 0 | `ArgumentList0` | `ArgumentList0` |
| 1 | `ArgumentListG1<>` | `ArgumentListS1` |
| 2 | `ArgumentListG2<,>` | `ArgumentListS2` |
| 3 | `ArgumentListG3<,,>` | `ArgumentListS3` |
| 4 | `ArgumentListG4<,,,>` | `ArgumentListS4` |
| 5-10 | (uses Simple) | `ArgumentListS5-S10` |

### ArgumentList Methods

| Method | Description |
|--------|-------------|
| `.Get<T>(index)` | Get argument at index |
| `.GetCancellationToken(index)` | Get cancellation token |
| `.Set<T>(index, val)` | Set argument value |
| `.Length` | Number of arguments |


## Handler Caching

```mermaid
flowchart TD
    Call["Method Call"] --> Check{"Handler in cache?"}

    Check -->|No| Create["CreateTypedHandler&lt;TUnwrapped&gt;(...)"]
    Create --> Store["Store in cache"]
    Store --> Execute

    Check -->|Yes| Cached["Use cached handler"]
    Cached --> Execute["Execute handler<br/>handler(invocation)"]
```


## Interceptor Chain

Multiple interceptors can be chained together:

```mermaid
flowchart TD
    Client --> Proxy
    Proxy --> Scheduling

    subgraph Scheduling ["SchedulingInterceptor"]
        S1["TaskFactoryResolver -> schedule on specific TaskFactory"]
        S2["NextInterceptor -> chain to another interceptor"]
    end

    Scheduling -->|NextInterceptor| Logging

    subgraph Logging ["LoggingInterceptor"]
        L1["Log method entry"]
        L2["Invoke intercepted method"]
        L3["Log method exit or error"]
    end

    Logging -->|"InvokeIntercepted()"| Target

    subgraph Target ["Target&nbsp;Service"]
        T1["Real implementation executes"]
    end
```


## Pass-Through vs Virtual Proxy

| Aspect | Pass-Through Proxy | Virtual Proxy |
|--------|-------------------|---------------|
| **Creation** | `Proxies.New(typeof(IService), interceptor, proxyTarget: realService)` | `Proxies.New(typeof(IService), interceptor)` |
| **ProxyTarget** | `!= null` | `== null` |
| **Flow** | Proxy → Interceptor → `InvokeIntercepted()` → Real Service | Proxy → Interceptor → Return default/mock |
| **Use cases** | Logging, Metrics, Caching, Retry logic | Mocking/Stubs, Default values, RPC client proxies, Lazy initialization |

```mermaid
flowchart LR
    subgraph PassThrough ["Pass-Through&nbsp;Proxy"]
        direction TB
        PT1["Proxy"] --> PT2["Interceptor"]
        PT2 -->|"InvokeIntercepted()"| PT3["Real Service"]
    end

    subgraph Virtual ["Virtual&nbsp;Proxy"]
        direction TB
        V1["Proxy"] --> V2["Interceptor"]
        V2 --> V3["Return default/mock<br/>(no target)"]
    end
```


## Typed vs Untyped Handlers

| Aspect | Typed Handlers (Default) | Untyped Handlers |
|--------|--------------------------|------------------|
| **Method** | `CreateTypedHandler<TUnwrapped>(invocation, methodDef)` | `CreateUntypedHandler(invocation, methodDef)` |
| **Setup** | Default behavior | `UsesUntypedHandlers = true` in constructor |
| **Return type** | `TUnwrapped` (e.g., `string` for `Task<string>`) | `object?` |
| **Performance** | One handler instantiation per unique return type | No generic instantiation overhead |
| **Use case** | Most use cases | `ComputeServiceInterceptor` for max performance |


## MethodDef Key Properties

For `Task<string> GreetAsync(string name, CancellationToken ct)`:

| Property | Value |
|----------|-------|
| `MethodInfo` | `GreetAsync` |
| `FullName` | `"MyNamespace.IGreetingService.GreetAsync"` |
| `ReturnType` | `typeof(Task<string>)` |
| `UnwrappedReturnType` | `typeof(string)` |
| `IsAsyncMethod` | `true` |
| `ReturnsTask` | `true` |
| `ReturnsValueTask` | `false` |
| `IsAsyncVoidMethod` | `false` |
| `CancellationTokenIndex` | `1` |
| `Parameters` | `[name: string, ct: CancellationToken]` |

### Helper Methods

| Method | Description |
|--------|-------------|
| `DefaultResult` | Completed Task with `default(T)` |
| `WrapResult(value)` | `Task.FromResult(value)` |
| `WrapAsyncInvokerResult(task)` | Proper `Task<T>` or `ValueTask<T>` |
| `InterceptedAsyncInvoker` | `Func<Invocation, Task<T>>` |
| `TargetAsyncInvoker` | `Func<object, Args, Task<T>>` |


## See Also

- [Interceptors and Proxies](./PartAP.md) - Main documentation
- [ArgumentList API](./PartAP-AL.md) - Working with method arguments
- [Proxy Generation](./PartAP-PG.md) - Source generator details
- [Built-in Interceptors](./PartAP-BI.md) - Ready-to-use interceptors
- [Cheat Sheet](./PartAP-CS.md) - Quick reference
