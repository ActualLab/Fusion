# CommandR Cheat Sheet

Quick reference for commands and handlers.

## Define Command

```cs
public record UpdateCartCommand(long CartId, Dictionary<long, long?> Updates)
    : ICommand<Unit>
{
    public UpdateCartCommand() : this(0, null!) { } // For deserialization
}
```

## Command Handler

In compute service interface:

```cs
public interface ICartService : IComputeService
{
    [CommandHandler]
    Task<Unit> UpdateCart(UpdateCartCommand command, CancellationToken cancellationToken = default);
}
```

Implementation (without Operations Framework):

```cs
public virtual async Task<Unit> UpdateCart(UpdateCartCommand command, CancellationToken cancellationToken)
{
    // Actual command logic
    var cart = await GetCart(command.CartId, cancellationToken);
    // ... apply updates ...

    // Invalidate affected compute methods (if not using Operations Framework)
    using (Invalidation.Begin()) {
        _ = GetCart(command.CartId, default);
    }

    return default;
}
```

> **Note:** For multi-host invalidation, use the Operations Framework pattern with `Invalidation.IsActive` instead. See [Operations Framework Cheat Sheet](Part05-CS.md).

## Execute Command

```cs
var commander = services.Commander();
await commander.Call(new UpdateCartCommand(cartId, updates), cancellationToken);
```

## Invalidation Patterns

Invalidate multiple methods:

```cs
using (Invalidation.Begin()) {
    _ = GetOrder(orderId, default);
    _ = GetOrderList(userId, default);
    _ = GetOrderCount(userId, default);
}
```

Conditional invalidation:

```cs
using (Invalidation.Begin()) {
    _ = GetOrder(orderId, default);
    if (statusChanged)
        _ = GetOrdersByStatus(oldStatus, default);
}
```

Check if invalidation scope is used:

```cs
using (var scope = Invalidation.Begin()) {
    _ = GetItem(itemId, default);
    if (scope.IsUsed) {
        // At least one invalidation happened
    }
}
```

## Standalone Command Handlers

For handlers outside compute services:

```cs
public class MyCommandHandler : ICommandHandler<MyCommand, Unit>
{
    public async Task OnCommand(MyCommand command, CommandContext context, CancellationToken ct)
    {
        // Handle command
    }
}

// Register
services.AddFusion().Commander.AddHandlers<MyCommandHandler>();
```
