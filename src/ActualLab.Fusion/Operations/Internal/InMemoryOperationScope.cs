using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Provides Operation for commands relying on in-memory state
/// to ensure they get <see cref="ICompletion"/>-based notifications.
/// </summary>
public sealed class InMemoryOperationScope : IOperationScope
{
    private IServiceProvider Services => CommandContext.Services;

    [field: AllowNull, MaybeNull]
    private ILogger Log => field ??= Services.LogFor(GetType());

    public CommandContext CommandContext { get; }
    public Operation Operation { get; }
    public bool IsTransient => true;
    public bool IsUsed => true;
    public bool? IsCommitted { get; private set; }
    public bool MustCreateOperation { get; set; }
    public bool CreatedOperation => false;
    public bool CreatedEvents { get; private set; }

    public static InMemoryOperationScope? TryGet(CommandContext context)
        => context.TryGetOperation()?.Scope as InMemoryOperationScope;

    public static InMemoryOperationScope GetOrCreate(CommandContext context)
    {
        var operation = context.TryGetOperation();
        if (operation is not null)
            return operation.Scope as InMemoryOperationScope
                ?? throw Errors.WrongOperationScopeType(typeof(InMemoryOperationScope), operation.Scope?.GetType());

        if (Invalidation.IsActive)
            throw Errors.NewOperationScopeIsRequestedFromInvalidationCode();

        return new InMemoryOperationScope(context.OutermostContext);
    }

    public static void Require(CommandContext? context = null)
    {
        context ??= CommandContext.GetCurrent();
        GetOrCreate(context);
    }

    public InMemoryOperationScope(CommandContext outermostContext)
    {
        CommandContext = outermostContext;
        Operation = Operation.NewTransient(this);
        Operation.Command = outermostContext.UntypedCommand;
        outermostContext.ChangeOperation(Operation);
    }

    public ValueTask DisposeAsync()
    {
        Close(false);
        return default;
    }

    public Task Commit(CancellationToken cancellationToken = default)
    {
        Close(true);
        if (IsCommitted == true)
            CreatedEvents = Operation.Events.Any(x => x.Value is not null);
        return Task.CompletedTask;
    }

    // Private methods

    private void Close(bool isCommitted)
    {
        if (IsCommitted.HasValue)
            return;

        IsCommitted = isCommitted;
        if (isCommitted)
            Operation.LoggedAt = CommandContext.Commander.Hub.Clocks.SystemClock.Now;
    }
}
