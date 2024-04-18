using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// This scope serves as the outermost, "catch-all" operation scope for
/// commands that don't use any other scopes.
/// </summary>
public sealed class TransientOperationScope : IOperationScope
{
    private ILogger? _log;

    private IServiceProvider Services => CommandContext.Services;
    private ILogger Log => _log ??= Services.LogFor(GetType());

    public CommandContext CommandContext { get; }
    public Operation Operation { get; }
    public bool IsTransient => true;
    public bool IsUsed => true;
    public bool? IsCommitted { get; private set; }

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
        return default;
    }

    public Task Commit(CancellationToken cancellationToken = default)
    {
        Close(true);
        return Task.CompletedTask;
    }

    // Private methods

    private void Close(bool isConfirmed)
    {
        if (IsCommitted.HasValue)
            return;

        IsCommitted = isConfirmed;
        if (isConfirmed)
            Operation.LoggedAt = CommandContext.Commander.Hub.Clocks.SystemClock.Now;
    }
}
