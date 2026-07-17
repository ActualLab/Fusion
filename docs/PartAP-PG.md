# Proxy Generation

The `ActualLab.Generators` package contains a Roslyn source generator that:

1. Scans for types implementing `IRequiresAsyncProxy` or `IRequiresFullProxy`
2. Generates a proxy class in the `ActualLabProxies` sub-namespace
3. The proxy implements `IProxy` and delegates all calls to the `Interceptor`

## Class Proxy vs Interface Proxy

The generator creates different proxies depending on whether you're proxying a **class** or an **interface**:

| Proxy Type | Inherits From | How It Calls Original |
|------------|---------------|----------------------|
| **Class proxy** | Original class | `base.Method(...)` |
| **Interface proxy** | `InterfaceProxy` | `((IInterface)ProxyTarget!).Method(...)` |

## Class Proxy Example

For a class like:

```cs
namespace YourNamespace;

public class TodoApi : IComputeService // IComputeService extends IRequiresAsyncProxy
{
    [ComputeMethod]
    public virtual Task<string[]> GetTodos(CancellationToken cancellationToken = default)
        => Task.FromResult(new[] { "Buy milk", "Write docs" });
}
```

The generator creates a **class proxy** (simplified):

```cs
namespace YourNamespace.ActualLabProxies;

public sealed class TodoApiProxy : TodoApi, IProxy  // Inherits from TodoApi
{
    // One static table per proxy type; the array position is the method's slot index
    private static readonly ProxyMethodTable __methodTable = new(typeof(TodoApiProxy), new[] {
        ProxyHelper.GetMethodInfo(typeof(TodoApi), "GetTodos", new[] { typeof(CancellationToken) }),
    });
    private Func<ArgumentList, Task<string[]>>? __cachedIntercepted0;
    private Func<Invocation, object?>? __handler0;  // Per-slot handler cache
    private InterceptorBinding? __binding;

    ProxyMethodTable IProxy.MethodTable => __methodTable;

    InterceptorBinding IProxy.Binding {
        get => ...;  // Returns __binding, or throws if the proxy isn't bound yet
        set {
            // The binding is assigned just once, right after the proxy construction
            if (__binding != null)
                throw Errors.InterceptorIsAlreadyBound();
            if (value.MethodTable != __methodTable)
                throw Errors.InvalidInterceptorBinding();
            __binding = value;
        }
    }

    public TodoApiProxy() : base() { }  // Calls base constructor

    public override Task<string[]> GetTodos(CancellationToken cancellationToken)
    {
        var intercepted = __cachedIntercepted0 ??= args => {
            if (args is ArgumentListG1<CancellationToken> ga)
                return base.GetTodos(ga.Item0);        // <-- Calls BASE class method
            var sa = (ArgumentListS1)args;
            return base.GetTodos((CancellationToken)sa.Item0);
        };

        var invocation = new Invocation(this, __methodTable, 0,  // 0 = this method's slot
            ArgumentList.New(cancellationToken), intercepted);
        var handler = __handler0 ?? InterceptorBinding.GetAndCacheHandler(ref __handler0, __binding, invocation);
        if (ReferenceEquals(handler, InterceptorBinding.NoHandler))
            return intercepted.Invoke(invocation.Arguments);  // Not intercepted -> typed direct call
        return (Task<string[]>)handler.Invoke(invocation)!;
    }
}
```

## Interface Proxy Example

For an interface like:

```cs
namespace YourNamespace;

public interface ITodoApi : IComputeService
{
    [ComputeMethod]
    Task<string[]> GetTodos(CancellationToken cancellationToken = default);
}
```

The generator creates an **interface proxy** (simplified):

```cs
namespace YourNamespace.ActualLabProxies;

public sealed class ITodoApiProxy : InterfaceProxy, ITodoApi, IProxy  // Inherits from InterfaceProxy
{
    private static readonly ProxyMethodTable __methodTable = new(typeof(ITodoApiProxy), new[] {
        ProxyHelper.GetMethodInfo(typeof(ITodoApi), "GetTodos", new[] { typeof(CancellationToken) }),
    });
    private Func<ArgumentList, Task<string[]>>? __cachedIntercepted0;
    private Func<Invocation, object?>? __handler0;
    private InterceptorBinding? __binding;

    ProxyMethodTable IProxy.MethodTable => __methodTable;

    InterceptorBinding IProxy.Binding {
        get => ...;   // Same as in the class proxy
        set { ... }
    }

    // No constructor needed - InterfaceProxy provides ProxyTarget property

    public Task<string[]> GetTodos(CancellationToken cancellationToken)
    {
        var intercepted = __cachedIntercepted0 ??= args => {
            if (args is ArgumentListG1<CancellationToken> ga)
                return ((ITodoApi)ProxyTarget!).GetTodos(ga.Item0);  // <-- Calls PROXY TARGET
            var sa = (ArgumentListS1)args;
            return ((ITodoApi)ProxyTarget!).GetTodos((CancellationToken)sa.Item0);
        };

        var invocation = new Invocation(this, __methodTable, 0,
            ArgumentList.New(cancellationToken), intercepted);
        var handler = __handler0 ?? InterceptorBinding.GetAndCacheHandler(ref __handler0, __binding, invocation);
        if (ReferenceEquals(handler, InterceptorBinding.NoHandler))
            return intercepted.Invoke(invocation.Arguments);
        return (Task<string[]>)handler.Invoke(invocation)!;
    }
}
```

> **Key Difference**: Class proxies call `base.Method()` to invoke the original implementation.
> Interface proxies call `((IInterface)ProxyTarget!).Method()` - the `ProxyTarget` is set when
> creating the proxy via `Proxies.New(type, interceptor, proxyTarget)`. If no target is provided,
> calling the intercepted delegate will fail (useful for pure interception without pass-through).

## Key Points About Generated Proxies

| Aspect | Description |
|--------|-------------|
| **Inheritance** | Class proxies inherit from the original class; interface proxies inherit from `InterfaceProxy` |
| **Method slots** | Every intercepted method gets a compile-time slot index in the static `ProxyMethodTable`; the table maps slots to `MethodInfo` and back |
| **Caching** | Each proxy instance caches the resolved handler per slot in a dedicated field, so a warm call is a field load + delegate invoke; the `InterceptorBinding` (one per interceptor + method table pair) shares resolved handlers across instances, and `SelectHandler` runs at most once per slot |
| **ArgumentList** | Arguments are packed into an `ArgumentList` struct (generic `ArgumentListG*` or struct-based `ArgumentListS*`) |
| **Invocation** | Contains proxy, method table + slot index (exposed as `Method`), arguments, and delegate to call the non-intercepted method |
| **Trimming** | `ModuleInitializer` and `CodeKeeper` ensure the proxy survives AOT compilation and trimming |

## Proxy Type Resolution

The `Proxies.New()` method finds generated proxy types by naming convention:

| Original Type | Generated Proxy Type |
|---------------|---------------------|
| `MyApp.IMyService` | `MyApp.ActualLabProxies.IMyServiceProxy` |
| `MyApp.MyService` | `MyApp.ActualLabProxies.MyServiceProxy` |
| `MyApp.Generic<T>` | `MyApp.ActualLabProxies.GenericProxy<T>` |

```cs
// These are equivalent:
var proxyType = Proxies.GetProxyType<IMyService>();
var proxyType = Proxies.GetProxyType(typeof(IMyService));

// Create proxy instance
var proxy = (IMyService)Proxies.New(typeof(IMyService), interceptor);
```
