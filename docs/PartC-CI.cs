using ActualLab.CommandR;
using ActualLab.CommandR.Commands;
using ActualLab.CommandR.Configuration;
using ActualLab.Fusion;
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming

// ReSharper disable once CheckNamespace
namespace Docs.PartCCI;

// ============================================================================
// PartC-CI.md snippets: Command Interfaces
// ============================================================================

#region PartCCI_ICommand
public interface ICommand;
#endregion

#region PartCCI_ICommandT
public interface ICommand<TResult> : ICommand;
#endregion

#region PartCCI_ICommandUsage
// Command returning a specific type
public record GetUserCommand(long UserId) : ICommand<User>;

// Command returning nothing (Unit is a void-like type)
public record DeleteUserCommand(long UserId) : ICommand<Unit>;
#endregion

#region PartCCI_IBackendCommand
public interface IBackendCommand : ICommand;
public interface IBackendCommand<TResult> : ICommand<TResult>, IBackendCommand;
#endregion

#region PartCCI_IBackendCommandUsage
// Internal command between backend services
public record SyncUserDataCommand(long UserId) : IBackendCommand<Unit>;

// Payment processing that must stay server-side
public record ProcessPaymentCommand(long OrderId, decimal Amount)
    : IBackendCommand<PaymentResult>;
#endregion

#region PartCCI_IOutermostCommand
public interface IOutermostCommand : ICommand;
#endregion

#region PartCCI_IOutermostCommandCheck
// In CommandContext.New():
// if (!isOutermost && (command is IOutermostCommand || Current?.UntypedCommand is IDelegatingCommand))
//     isOutermost = true;
#endregion

#region PartCCI_IOutermostCommandUsage
// This command should never inherit state from a parent command
public record ResetSystemStateCommand : IOutermostCommand, ICommand<Unit>;
#endregion

#region PartCCI_IDelegatingCommand
public interface IDelegatingCommand : IOutermostCommand;
public interface IDelegatingCommand<TResult> : ICommand<TResult>, IDelegatingCommand;
#endregion

#region PartCCI_IDelegatingCommandCheck
// In CommandContext.New():
// if (Current?.UntypedCommand is IDelegatingCommand)
//     isOutermost = true;
#endregion

#region PartCCI_IDelegatingCommandBypass
// In InvalidatingCommandCompletionHandler.IsRequired():
// if (command is null or IDelegatingCommand) {
//     finalHandler = null;
//     return false;  // No invalidation needed
// }
#endregion

#region PartCCI_IDelegatingCommandUsage
public record ProcessBatchOrdersCommand(long[] OrderIds)
    : IDelegatingCommand<BatchResult>;

// [CommandHandler]
// public virtual async Task<BatchResult> ProcessBatch(
//     ProcessBatchOrdersCommand command, CancellationToken ct)
// {
//     // No Invalidation.IsActive check needed!
//     var results = new List<OrderResult>();
//     foreach (var orderId in command.OrderIds) {
//         // Each ProcessOrderCommand runs as its own outermost command
//         // with full pipeline, separate transaction, and invalidation
//         var result = await Commander.Call(new ProcessOrderCommand(orderId), ct);
//         results.Add(result);
//     }
//     return new BatchResult(results);
// }
#endregion

#region PartCCI_ISystemCommand
public interface ISystemCommand : ICommand<Unit>;
#endregion

#region PartCCI_ISystemCommandCheck
// In CommandServiceInterceptor:
// if (!ReferenceEquals(invocationCommand, contextCommand) && contextCommand is not ISystemCommand) {
//     throw Errors.DirectCommandHandlerCallsAreNotAllowed();
// }
#endregion

#region PartCCI_ISystemCommandUsage
// Used internally by Operations Framework
public record Completion<TCommand>(Operation Operation) : ISystemCommand
    where TCommand : class, ICommand;
#endregion

#region PartCCI_IPreparedCommand
public interface IPreparedCommand : ICommand
{
    Task Prepare(CommandContext context, CancellationToken cancellationToken);
}
#endregion

#region PartCCI_IPreparedCommandHandler
// In PreparedCommandHandler:
// if (command is IPreparedCommand preparedCommand)
//     await preparedCommand.Prepare(context, cancellationToken);
// await context.InvokeRemainingHandlers(cancellationToken);
#endregion

#region PartCCI_IPreparedCommandUsage
public record CreateOrderCommand(long UserId, List<OrderItem> Items)
    : IPreparedCommand, ICommand<Order>
{
    public Task Prepare(CommandContext context, CancellationToken ct)
    {
        // Validation - fails before any handler runs
        if (Items.Count == 0)
            throw new ValidationException("Order must have at least one item");

        if (Items.Any(i => i.Quantity <= 0))
            throw new ValidationException("All items must have positive quantity");

        // Normalization
        foreach (var item in Items)
            item.ProductId = item.ProductId.Trim().ToUpperInvariant();

        return Task.CompletedTask;
    }
}
#endregion

#region PartCCI_ILocalCommand
public interface ILocalCommand : ICommand
{
    Task Run(CommandContext context, CancellationToken cancellationToken);
}

public interface ILocalCommand<T> : ICommand<T>, ILocalCommand;
#endregion

#region PartCCI_ILocalCommandRunner
// In LocalCommandRunner:
// if (command is ILocalCommand localCommand)
//     await localCommand.Run(context, cancellationToken);
#endregion

#region PartCCI_ILocalCommandUsage
public record LogMessageCommand(string Message) : ILocalCommand
{
    public Task Run(CommandContext context, CancellationToken ct)
    {
        var logger = context.Services.GetRequiredService<ILogger<LogMessageCommand>>();
        logger.LogInformation("{Message}", Message);
        return Task.CompletedTask;
    }
}
#endregion

#region PartCCI_LocalCommandFactory
// Action-based (returns Unit)
// var cmd = LocalCommand.New(() => Console.WriteLine("Hello"));
// var cmd = LocalCommand.New(ct => SomeAsyncWork(ct));
// var cmd = LocalCommand.New((ctx, ct) => WorkWithContext(ctx, ct));

// Func-based (returns a value)
// var cmd = LocalCommand.New(() => 42);
// var cmd = LocalCommand.New(ct => GetValueAsync(ct));
// var cmd = LocalCommand.New<string>((ctx, ct) => GetResultAsync(ctx, ct));

// await commander.Call(cmd);
#endregion

#region PartCCI_LocalCommandFactoryUsage
// Execute a cleanup action through the command pipeline
// await commander.Call(LocalCommand.New(async ct => {
//     await cache.ClearAsync(ct);
//     await tempFiles.DeleteAllAsync(ct);
// }));
#endregion

#region PartCCI_IEventCommand
public interface IEventCommand : ICommand<Unit>
{
    string ChainId { get; init; }
}
#endregion

#region PartCCI_IEventCommandHowItWorks
// For event commands, CommandHandlerResolver builds a separate handler chain for each non-filter handler:
// var handlerChains = (
//     from nonFilterHandler in nonFilterHandlers
//     let handlerSubset = handlers.Where(h => h.IsFilter || h == nonFilterHandler).ToArray()
//     select KeyValuePair.Create(nonFilterHandler.Id, new CommandHandlerChain(handlerSubset))
// ).ToImmutableDictionary();

// RunEvent() clones the command for each handler chain:
// foreach (var (chainId, _) in handlerChains) {
//     var chainCommand = MemberwiseCloner.Invoke(command);
//     ChainIdSetter.Invoke(chainCommand, chainId);
//     callTasks[i++] = this.Call(chainCommand, context.IsOutermost, cancellationToken);
// }
// await Task.WhenAll(callTasks);
#endregion

#region PartCCI_IEventCommandUsage
public record OrderCreatedEvent(Order Order) : IEventCommand
{
    public string ChainId { get; init; } = "";
}

// Each handler gets its own parallel execution chain
public class NotificationHandlers
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

    [CommandHandler]
    public Task NotifyWarehouse(OrderCreatedEvent evt, CancellationToken ct)
    {
        // Notify warehouse
        return Task.CompletedTask;
    }
}

// When you call:
// await commander.Call(new OrderCreatedEvent(order));

// Commander internally creates 3 parallel calls:
// - OrderCreatedEvent { ChainId = "SendEmail handler id" }
// - OrderCreatedEvent { ChainId = "UpdateAnalytics handler id" }
// - OrderCreatedEvent { ChainId = "NotifyWarehouse handler id" }
#endregion

#region PartCCI_IComputeService
public interface IComputeService : ICommandService;
#endregion

#region PartCCI_ISessionCommand
public interface ISessionCommand : ICommand
{
    Session Session { get; init; }
}

public interface ISessionCommand<TResult> : ICommand<TResult>, ISessionCommand;
#endregion

#region PartCCI_RpcDefaultSessionReplacer
// In RpcDefaultSessionReplacer.Create<T>():
// if (typeof(ISessionCommand).IsAssignableFrom(p0Type))
//     return call => {
//         if (!HasSessionBoundRpcConnection(call, out var connection))
//             return next.Invoke(call);
//
//         var command = (ISessionCommand?)args.Get0Untyped();
//         var session = command.Session;
//         if (session.IsDefault())
//             command.SetSession(connection.Session);  // Replace with connection's session
//         else
//             session.RequireValid();  // Validate non-default sessions
//         return next.Invoke(call);
//     };
#endregion

#region PartCCI_RpcDefaultSessionReplacerReg
// In FusionWebServerBuilder constructor:
// rpc.AddMiddleware(_ => new RpcDefaultSessionReplacer());
#endregion

#region PartCCI_ISessionCommandUsage
public record UpdateProfileCommand(string Name, string Bio)
    : ISessionCommand<Unit>
{
    public required Session Session { get; init; }
}

// Client-side: can use Session.Default, server will replace it
// await commander.Call(new UpdateProfileCommand("John", "Developer") {
//     Session = Session.Default  // Will be replaced by RpcDefaultSessionReplacer
// });

// Server-side handler:
// [CommandHandler]
// public virtual async Task<Unit> UpdateProfile(
//     UpdateProfileCommand command, CancellationToken ct)
// {
//     // command.Session is now the actual session from the RPC connection
//     var user = await Auth.GetUser(command.Session, ct);
//     if (user == null)
//         throw new UnauthorizedAccessException();
//
//     // Update profile...
//     return default;
// }
#endregion

#region PartCCI_CombiningInterfaces
// Backend-only, session-bound command with validation
public record SecureTransactionCommand(decimal Amount)
    : IBackendCommand<TransactionResult>,
      ISessionCommand<TransactionResult>,
      IPreparedCommand
{
    public required Session Session { get; init; }

    public Task Prepare(CommandContext context, CancellationToken ct)
    {
        if (Amount <= 0)
            throw new ArgumentException("Amount must be positive");
        return Task.CompletedTask;
    }
}
#endregion

// ============================================================================
// Helper types for snippets (not exported as snippets)
// ============================================================================

public record User;
public record PaymentResult;
public record BatchResult(List<OrderResult> Results);
public record OrderResult;
public record Order;
public class OrderItem
{
    public string ProductId { get; set; } = "";
    public int Quantity { get; set; }
}
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
public record TransactionResult;
public record Operation;
