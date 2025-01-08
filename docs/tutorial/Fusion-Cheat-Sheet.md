# Fusion Cheat Sheet

## Compute Services

Compute service interface:
```cs
// IComputeService is just an optional tagging interface.
// Nevertheless, we highly recommend to "implement" it -
// it allows you to use a few extension methods, such as
// .GetServices() and .GetCommander().
public interface ICartService : IComputeService
{
    // When applied to interface method, this attribute 
    // is "inherited" by the implementation
    [ComputeMethod]
    Task<List<Order>> GetOrders(long cartId, CancellationToken cancellationToken = default);
}
```
Compute service implementation:
```cs
public class CartService : ICartService 
{
    // The method must be virtual + return Task<T>
    public virtual async Task<List<Order>> GetOrders(long cartId, CancellationToken cancellationToken)
    {
        // Implementation goes here
    }
}    
```

Add invalidation logic (after any code that changes the result of what's invalidated inside the `using` block below):
```cs
using (Invalidation.Begin()) {
    // Whatever compute method calls you make inside this block
    // are invalidating calls. Instead of running the actual method 
    // code they invalidate the result of this call.
    // They always complete synchronously, and you can pass
    // "default" instead of any CancellationToken here.
    _ = GetOrders(cartId, default);
}    
```

Register compute service:
```cs
fusion = services.AddFusion(); // services is IServiceCollection
fusion.AddService<ICartService, CartService>();
```

## Compute Service Clients

Configure Fusion RPC client (this has to be done once in a code that configures client-side `IServiceProvider`):
```cs
var baseUri = new Uri("http://localhost:5005");

var fusion = services.AddFusion();
fusion.Rpc.AddWebSocketClient(baseUri);
```

Register Compute Service client:
```cs
fusion.AddClient<ICartService>();
```

Use use it:
```cs
// Just call it the same way you call the original one.
// Any calls that are expected to produce the same result
// as the previous call with the same arguments will
// be resolved via locally cached IComputed.
// When a IComputed gets invalidated on the server,
// Fusion will invalidate its replica on every client.
```

## Commander

Declare command type:
```cs
// You don't have to use records, but we recommend to use
// immutable types for commands and outputs of compute methods
public record UpdateCartCommand(long CartId, Dictionary<long, long?> Updates) 
    : ICommand<Unit> // Unit is command's return type; you can use any other
{
    // Compatibility: Newtonsoft.Json needs this constructor to deserialize the record
    public UpdateCartCommand() : this(0, null!) { }
}
```

Add command handler in the compute service interface:
```cs
public interface ICartService : IComputeService
{
    // ...
    [CommandHandler] // This attribute is also "inherited" by the impl.
    Task<Unit> UpdateCart(UpdateCartCommand command, CancellationToken cancellationToken = default);
```

Add command handler implementation:
```cs
public class CartService : ICartService 
{
    // Must be virtual + return Task<T> for ICommand<T>;
    // Command must be its first argument; other arguments are resolved
    // from DI container - except CancellationToken, which is 
    // passed directly.
    public virtual Task<Unit> UpdateCart(UpdateCartCommand command, CancellationToken cancellationToken) 
    {
        if (Invalidation.IsActive) {
            // Write the invalidation logic for this command here.
            //
            // A set of command handlers registered by Fusion will
            // "retry" this handler inside the invalidation block
            // once its "normal" logic completes successfully.
            // Moreover, they'll run this block on every node on the 
            // cluster if you use multi-host invalidation.
            return default;
        }

        // Command handler code goes here
    }

```

Register command handler:
```
// Nothing is needed for handlers declared inside compute services
```

## Working with `IComputed`

Capture:
```cs
var computed = await Computed.Capture(() => service.ComputeMethod(args, cancellationToken));
```

Check whether `IComputed` is still consistent:
```cs
if (computed.IsConsistent()) {
    // ...
}
```

Await for invalidation:
```cs
// Always pass CancellationToken here, otherwise you'll
// end up with a memory leak due to growing number of
// event handler registrations. They'll be gone on
// invalidation, of course, but what if it never happens?
await computed.WhenInvalidated(cancellationToken);
// Or
computed.Invalidated += c => Console.WriteLine("Invalidated!");
```

To be continued.

#### [&gt; Back to Tutorial](./README.md) | [Documentation Home](../index.md)
