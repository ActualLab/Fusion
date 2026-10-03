using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Provides Operation for commands relying on in-memory state
/// to ensure they get <see cref="ICompletion"/>-based notifications.
/// </summary>
public sealed class TransientOperationScope : IOperationScope
{
    private IServiceProvider Services => CommandContext.Services;
    private ILogger Log => field ??= Services.LogFor(GetType());

    public CommandContext CommandContext { get; }
    public Operation Operation { get; }
    public bool IsTransient => true;
    public bool IsUsed => true;
    public bool? IsCommitted { get; private set; }
    public OperationStoreMode? StoreMode { get; set; }
    public bool HasStoredOperation => false;
    public bool HasStoredEvents { get; private set; }
    public ImmutableList<Func<IOperationScope, Task>> CompletionHandlers { get; set; }
        = ImmutableList<Func<IOperationScope, Task>>.Empty;

    public static TransientOperationScope? TryGet(CommandContext context)
        => context.TryGetOperation()?.Scope as TransientOperationScope;

    public static TransientOperationScope GetOrCreate(CommandContext context)
    {
        var operation = context.TryGetOperation();
        if (operation is not null)
            return operation.Scope as TransientOperationScope
                ?? throw Errors.WrongOperationScopeType(typeof(TransientOperationScope), operation.Scope?.GetType());

        if (Invalidation.IsActive)
            throw Errors.NewOperationScopeIsRequestedFromInvalidationCode();

        return new TransientOperationScope(context.OutermostContext);
    }

    public static void Require(CommandContext? context = null)
    {
        context ??= CommandContext.GetCurrent();
        GetOrCreate(context);
    }

    public TransientOperationScope(CommandContext outermostContext)
    {
        CommandContext = outermostContext;
        Operation = Operation.NewTransient(this);
        Operation.Command = outermostContext.UntypedCommand;
        outermostContext.ChangeOperation(Operation);
    }

    public ValueTask DisposeAsync()
    {
        Close(false);
        return CompletionHandlers.IsEmpty
            ? default
            : CompleteAsync();

        async ValueTask CompleteAsync() {
            foreach (var completionHandler in CompletionHandlers) {
                try {
                    await completionHandler.Invoke(this).ConfigureAwait(false);
                }
                catch (Exception e) {
                    Log.LogError(e, "DisposeAsync: one of completion handlers failed");
                }
            }
        }
    }

    public async Task Commit(CancellationToken cancellationToken = default)
    {
        await DeferredInvalidationHelper.SetInvalidationsAndStoreMode(Operation).ConfigureAwait(false);
        Close(true);
        if (IsCommitted == true)
            HasStoredEvents = Operation.Events.Any(x => x.Value is not null);
    }

    public Task TryCompleteStoredEvent(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

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
