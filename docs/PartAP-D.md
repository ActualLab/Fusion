# Interceptors and Proxies: Diagrams

Text-based diagrams for the concepts introduced in [Interceptors and Proxies](PartAP.md).

## Proxy Call Flow

How a method call flows through the proxy system:

```
    Client Code
        │
        │ proxy.GreetAsync("World")
        ▼
┌───────────────────────────────────────────────────────────────────┐
│                        IGreetingServiceProxy                      │
├───────────────────────────────────────────────────────────────────┤
│  1. Create Invocation(proxy, method, arguments, intercepted)      │
│  2. Call interceptor.Intercept<TResult>(invocation)               │
└───────────────────────────────────────────────────────────────────┘
        │
        ▼
┌───────────────────────────────────────────────────────────────────┐
│                          Interceptor                              │
├───────────────────────────────────────────────────────────────────┤
│  1. GetHandler(invocation) - lookup or create                     │
│  2. handler(invocation) - execute your logic                      │
└───────────────────────────────────────────────────────────────────┘
        │
        │ (if pass-through proxy)
        ▼
┌───────────────────────────────────────────────────────────────────┐
│                         Target Service                            │
├───────────────────────────────────────────────────────────────────┤
│  invocation.InvokeIntercepted() -> real implementation            │
└───────────────────────────────────────────────────────────────────┘
        │
        │ result bubbles back up
        ▼
    Client receives result
```

## Proxy Generation at Compile Time

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Compile Time                                   │
└─────────────────────────────────────────────────────────────────────────────┘

    Your Interface                    Source Generator                Generated Proxy
┌─────────────────────┐          ┌─────────────────────┐          ┌──────────────────────┐
│ interface           │          │ ActualLab.Generators│          │ class                │
│ IGreetingService    │ ───────► │                     │ ───────► │ IGreetingServiceProxy│
│ : IRequiresAsyncProxy│  scans  │ ProxyGenerator      │ emits    │ : InterfaceProxy     │
│                     │          │                     │          │ , IGreetingService   │
│ Task<string>        │          │                     │          │ , IProxy             │
│   GreetAsync(...)   │          │                     │          │                      │
└─────────────────────┘          └─────────────────────┘          └──────────────────────┘
```

## Invocation Structure

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Invocation                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐         │
│   │     Proxy       │    │     Method      │    │   Arguments     │         │
│   │ (IGreeting-     │    │  (MethodInfo)   │    │ (ArgumentList)  │         │
│   │  ServiceProxy)  │    │                 │    │                 │         │
│   └─────────────────┘    └─────────────────┘    └─────────────────┘         │
│                                                         │                   │
│   ┌─────────────────┐                                   ▼                   │
│   │ Intercepted-    │                        ┌─────────────────────┐        │
│   │ Delegate        │                        │ .Get<T>(index)      │        │
│   │ (for pass-thru) │                        │ .GetCancellationTo- │        │
│   └─────────────────┘                        │   ken(index)        │        │
│                                              │ .Set<T>(index, val) │        │
│   ┌─────────────────┐                        │ .Length             │        │
│   │ InterfaceProxy- │                        └─────────────────────┘        │
│   │ Target          │                                                       │
│   │ (real service)  │                                                       │
│   └─────────────────┘                                                       │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Handler Caching

```
                         Method Call
                              │
                              ▼
                    ┌─────────────────────┐
                    │  Handler in cache?  │
                    └──────────┬──────────┘
                               │
              ┌────────────────┴────────────────┐
              │ No                              │ Yes
              ▼                                 ▼
    ┌─────────────────────┐           ┌─────────────────────┐
    │ CreateTypedHandler  │           │    Use cached       │
    │ <TUnwrapped>(...)   │           │    handler          │
    └──────────┬──────────┘           └──────────┬──────────┘
               │                                 │
               ▼                                 │
    ┌─────────────────────┐                      │
    │   Store in cache    │                      │
    └──────────┬──────────┘                      │
               │                                 │
               └────────────────┬────────────────┘
                                │
                                ▼
                    ┌─────────────────────┐
                    │  Execute handler    │
                    │  handler(invocation)│
                    └─────────────────────┘
```

## Interceptor Chain

Multiple interceptors can be chained together:

```
    Client
      │
      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Proxy                                          │
└─────────────────────────────────────────────────────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       SchedulingInterceptor                                 │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │  • TaskFactoryResolver -> schedule on specific TaskFactory            │  │
│  │  • NextInterceptor -> chain to another interceptor                    │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
      │
      │ NextInterceptor
      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        LoggingInterceptor                                   │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │  • Log method entry                                                   │  │
│  │  • Invoke intercepted method                                          │  │
│  │  • Log method exit or error                                           │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
      │
      │ InvokeIntercepted()
      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Target Service                                     │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │  Real implementation executes                                         │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Proxy Type Hierarchy

```
                           IRequiresAsyncProxy
                                   │
                                   │ extends
                                   ▼
                           IRequiresFullProxy
                          (also intercepts sync)


                              InterfaceProxy
                                   │
                                   │ extends
                                   ▼
┌───────────────────────────────────────────────────────────────────────────┐
│                         IGreetingServiceProxy                             │
├───────────────────────────────────────────────────────────────────────────┤
│  implements: IGreetingService, IProxy                                     │
│                                                                           │
│  Fields:                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  __interceptor: Interceptor                                         │  │
│  │  __cachedIntercepted0: Func<ArgumentList, Task<string>>             │  │
│  │  __cachedIntercept0: Func<Invocation, Task<string>>                 │  │
│  │  ProxyTarget: object? (from InterfaceProxy)                         │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                           │
│  Methods:                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │  Task<string> GreetAsync(string name, CancellationToken ct)        ─│  │
│  │  {                                                                  │  │
│  │      var invocation = new Invocation(this, method, args, delegate); │  │
│  │      return __cachedIntercept0(invocation);                         │  │
│  │  }                                                                  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────────────┘
```

## ArgumentList Variants

```
                          ArgumentList.New<T0, T1>(arg0, arg1)
                                         │
                                         ▼
                              ┌─────────────────────┐
                              │   UseGenerics?      │
                              └──────────┬──────────┘
                                         │
                    ┌────────────────────┴────────────────────┐
                    │ Yes (1-4 args)                          │ No (or 5+ args)
                    ▼                                         ▼
        ┌─────────────────────────┐              ┌─────────────────────────┐
        │  ArgumentListG2<T0,T1>  │              │    ArgumentListS2       │
        ├─────────────────────────┤              ├─────────────────────────┤
        │  • Generic storage      │              │  • object? storage      │
        │  • No boxing for        │              │  • Boxing for value     │
        │    value types          │              │    types                │
        │  • Faster access        │              │  • All platforms        │
        └─────────────────────────┘              └─────────────────────────┘


    ArgumentList Types by Argument Count:
    ┌──────────┬────────────────────────┬────────────────────────┐
    │  Count   │    Generic Type        │    Simple Type         │
    ├──────────┼────────────────────────┼────────────────────────┤
    │    0     │    ArgumentList0       │    ArgumentList0       │
    │    1     │    ArgumentListG1<>    │    ArgumentListS1      │
    │    2     │    ArgumentListG2<,>   │    ArgumentListS2      │
    │    3     │    ArgumentListG3<,,>  │    ArgumentListS3      │
    │    4     │    ArgumentListG4<,,,> │    ArgumentListS4      │
    │   5-10   │    (uses Simple)       │    ArgumentListS5-S10  │
    └──────────┴────────────────────────┴────────────────────────┘
```

## Pass-Through vs Virtual Proxy

```
┌────────────────────────────────────┬────────────────────────────────────┐
│       Pass-Through Proxy           │         Virtual Proxy              │
├────────────────────────────────────┼────────────────────────────────────┤
│                                    │                                    │
│   Proxies.New(                     │   Proxies.New(                     │
│     typeof(IService),              │     typeof(IService),              │
│     interceptor,                   │     interceptor                    │
│     proxyTarget: realService)      │     /* no target */                │
│                                    │   )                                │
│   ProxyTarget != null              │   ProxyTarget == null              │
│                                    │                                    │
│   ┌──────────────────────────┐     │   ┌──────────────────────────┐     │
│   │ Proxy                    │     │   │ Proxy                    │     │
│   │   │                      │     │   │   │                      │     │
│   │   ▼                      │     │   │   ▼                      │     │
│   │ Interceptor              │     │   │ Interceptor              │     │
│   │   │                      │     │   │   │                      │     │
│   │   │ InvokeIntercepted()  │     │   │   │ (no target to call)  │     │
│   │   ▼                      │     │   │   ▼                      │     │
│   │ Real Service             │     │   │ Return default/mock      │     │
│   └──────────────────────────┘     │   └──────────────────────────┘     │
│                                    │                                    │
│   Use cases:                       │   Use cases:                       │
│   • Logging                        │   • Mocking/Stubs                  │
│   • Metrics                        │   • Default values                 │
│   • Caching                        │   • RPC client proxies             │
│   • Retry logic                    │   • Lazy initialization            │
│                                    │                                    │
└────────────────────────────────────┴────────────────────────────────────┘
```

## Typed vs Untyped Handlers

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Typed Handlers (Default)                         │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   CreateTypedHandler<TUnwrapped>(invocation, methodDef)                     │
│                                                                             │
│   • TUnwrapped = unwrapped return type (e.g., string for Task<string>)      │
│   • Type-safe result handling                                               │
│   • One handler instantiation per unique return type                        │
│   • Best for most use cases                                                 │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                            Untyped Handlers                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   UsesUntypedHandlers = true;  // Set in constructor                        │
│   CreateUntypedHandler(invocation, methodDef)                               │
│                                                                             │
│   • Returns object?                                                         │
│   • No generic instantiation overhead                                       │
│   • Used by ComputeServiceInterceptor for max performance                   │
│   • Requires manual type handling                                           │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## MethodDef Key Properties

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              MethodDef                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   For: Task<string> GreetAsync(string name, CancellationToken ct)           │
│                                                                             │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │  MethodInfo          = GreetAsync                                   │   │
│   │  FullName            = "MyNamespace.IGreetingService.GreetAsync"    │   │
│   │  ReturnType          = typeof(Task<string>)                         │   │
│   │  UnwrappedReturnType = typeof(string)                               │   │
│   │  IsAsyncMethod       = true                                         │   │
│   │  ReturnsTask         = true                                         │   │
│   │  ReturnsValueTask    = false                                        │   │
│   │  IsAsyncVoidMethod   = false                                        │   │
│   │  CancellationTokenIndex = 1                                         │   │
│   │  Parameters          = [name: string, ct: CancellationToken]        │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│   Helper Methods:                                                           │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │  DefaultResult                  -> completed Task with default(T)   │   │
│   │  WrapResult(value)              -> Task.FromResult(value)           │   │
│   │  WrapAsyncInvokerResult(task)   -> proper Task<T> or ValueTask<T>   │   │
│   │  InterceptedAsyncInvoker        -> Func<Invocation, Task<T>>        │   │
│   │  TargetAsyncInvoker             -> Func<object, Args, Task<T>>      │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## See Also

- [Interceptors and Proxies](./PartAP.md) - Main documentation
- [ArgumentList API](./PartAP-AL.md) - Working with method arguments
- [Proxy Generation](./PartAP-PG.md) - Source generator details
- [Built-in Interceptors](./PartAP-BI.md) - Ready-to-use interceptors
- [Cheat Sheet](./PartAP-CS.md) - Quick reference
