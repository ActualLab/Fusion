using ActualLab.CommandR;
using ActualLab.CommandR.Commands;
using ActualLab.CommandR.Configuration;
using ActualLab.Fusion;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartCMC;

// ============================================================================
// PartC-MC.md snippets: MediatR Comparison
// ============================================================================

#region PartCMC_MediatRPipeline
// Pipeline behavior applies to all commands
// public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
// {
//     public async Task<TResponse> Handle(TRequest request, ...)
//     {
//         // Runs for ALL commands
//     }
// }
#endregion

#region PartCMC_CommandRPipeline
// Filter handler can target specific command types
// [CommandHandler(Priority = 10, IsFilter = true)]
// public async Task OnPaymentCommand(IPaymentCommand command, CancellationToken ct)
// {
//     // Runs only for IPaymentCommand and its derivatives
//     await context.InvokeRemainingHandlers(ct);
// }
#endregion

#region PartCMC_CommandContext
// [CommandHandler]
// public async Task<Order> CreateOrder(CreateOrderCommand command, CancellationToken ct)
// {
//     var context = CommandContext.GetCurrent();

//     // Access scoped services
//     var db = context.Services.GetRequiredService<AppDbContext>();

//     // Store data for other handlers
//     context.Items["StartTime"] = DateTime.UtcNow;

//     // Access outer context for nested commands
//     var rootContext = context.OutermostContext;

//     // ...
// }
#endregion

#region PartCMC_MediatRHandler
// public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Order>
// {
//     public async Task<Order> Handle(CreateOrderCommand request, CancellationToken ct)
//     {
//         // ...
//     }
// }
#endregion

#region PartCMC_CommandRHandler
public class OrderHandlers
{
    [CommandHandler] // Just add an attribute
    public async Task<Order> CreateOrder(
        CreateOrderCommand command,
        IOrderService orderService, // Resolved from DI
        CancellationToken ct)
    {
        return default!;
    }
}
#endregion

#region PartCMC_CommandServiceInterceptor
public class OrderService : ICommandService
{
    [CommandHandler]
    public virtual async Task<Order> CreateOrder(
        CreateOrderCommand command, CancellationToken ct)
    {
        return default!;
    }
}

// Registration creates a proxy
// commander.AddService<OrderService>();

// Correct - use Commander:
// await commander.Call(new CreateOrderCommand(...), ct);

// Direct calls throw NotSupportedException!
// await orderService.CreateOrder(new CreateOrderCommand(...), ct); // Throws!
#endregion

#region PartCMC_MediatRRegistration
// services.AddMediatR(cfg => {
//     cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
// });
#endregion

#region PartCMC_CommandRRegistration
// Add specific handlers
// services.AddCommander()
//     .AddHandlers<OrderHandlers>()
//     .AddHandlers<PaymentHandlers>();

// Or add command services (creates proxies)
// services.AddCommander()
//     .AddService<OrderService>();
#endregion

#region PartCMC_MediatRMigrationBefore
// public record CreateOrderCommand(long UserId, List<OrderItem> Items)
//     : IRequest<Order>;

// public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Order>
// {
//     private readonly IOrderRepository _repository;

//     public CreateOrderHandler(IOrderRepository repository)
//     {
//         _repository = repository;
//     }

//     public async Task<Order> Handle(CreateOrderCommand request, CancellationToken ct)
//     {
//         return await _repository.Create(request.UserId, request.Items, ct);
//     }
// }
#endregion

#region PartCMC_MediatRMigrationAfter
public record CreateOrderCommand(long UserId, List<OrderItem> Items)
    : ICommand<Order>;

public class OrderHandlersForMigration
{
    private readonly IOrderRepository _repository;

    public OrderHandlersForMigration(IOrderRepository repository)
    {
        _repository = repository;
    }

    [CommandHandler]
    public async Task<Order> CreateOrder(CreateOrderCommand command, CancellationToken ct)
    {
        return await _repository.Create(command.UserId, command.Items, ct);
    }
}

// Registration
// services.AddScoped<OrderHandlers>();
// services.AddCommander().AddHandlers<OrderHandlers>();
#endregion

#region PartCMC_MediatRValidationBefore
// public class ValidationBehavior<TRequest, TResponse>
//     : IPipelineBehavior<TRequest, TResponse>
// {
//     public async Task<TResponse> Handle(
//         TRequest request,
//         RequestHandlerDelegate<TResponse> next,
//         CancellationToken ct)
//     {
//         // Validate
//         if (request is IValidatable v)
//             v.Validate();

//         return await next();
//     }
// }
#endregion

#region PartCMC_MediatRValidationAfter
public class ValidationHandler
{
    [CommandHandler(Priority = 1000, IsFilter = true)]
    public async Task OnCommand(ICommand command, CancellationToken ct)
    {
        if (command is IValidatable v)
            v.Validate();

        var context = CommandContext.GetCurrent();
        await context.InvokeRemainingHandlers(ct);
    }
}

// Registration
// services.AddSingleton<ValidationHandler>();
// services.AddCommander().AddHandlers<ValidationHandler>();
#endregion

#region PartCMC_MediatRNotificationBefore
// public record OrderCreatedNotification(Order Order) : INotification;

// public class OrderCreatedHandler1 : INotificationHandler<OrderCreatedNotification>
// {
//     public Task Handle(OrderCreatedNotification notification, CancellationToken ct)
//     {
//         // Send email
//     }
// }

// public class OrderCreatedHandler2 : INotificationHandler<OrderCreatedNotification>
// {
//     public Task Handle(OrderCreatedNotification notification, CancellationToken ct)
//     {
//         // Update analytics
//     }
// }
#endregion

#region PartCMC_MediatRNotificationAfter
public record OrderCreatedEvent(Order Order) : IEventCommand
{
    public string ChainId { get; init; } = "";
}

// Multiple handlers will execute in parallel
public class OrderEventHandlers
{
    [CommandHandler]
    public Task SendEmail(OrderCreatedEvent evt, CancellationToken ct)
    {
        // Send email
        return Task.CompletedTask;
    }

    [CommandHandler]
    public Task UpdateAnalytics(OrderCreatedEvent evt, CancellationToken ct)
    {
        // Update analytics
        return Task.CompletedTask;
    }
}
#endregion

// Helper types
public record Order;
public class OrderItem { }
public interface IOrderService { }
public interface IOrderRepository
{
    Task<Order> Create(long userId, List<OrderItem> items, CancellationToken ct);
}
public interface IValidatable
{
    void Validate();
}
