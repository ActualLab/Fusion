using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// This scope serves as the outermost, "catch-all" operation scope for
/// commands that don't use any other scopes.
/// </summary>
public sealed class TransientOperationScope : AsyncDisposableBase, IOperationScope
{
    private ILogger? _log;

    private IServiceProvider Services { get; }
    private ILogger Log => _log ??= Services.LogFor(GetType());

    public CommandContext CommandContext { get; }
    public Operation Operation { get; }
    public bool IsTransient => true;
    public bool IsUsed { get; private set; }
    public bool IsClosed { get; private set; }
    public bool? IsConfirmed { get; private set; }

    public TransientOperationScope(IServiceProvider services)
    {
        Services = services;
        CommandContext = CommandContext.GetCurrent();
        Operation = Operation.NewTransient(this);
    }

    protected override ValueTask DisposeAsyncCore()
    {
        IsConfirmed ??= true;
        IsClosed = true;
        return default;
    }

    protected override void Dispose(bool disposing)
    {
        // Intentionally ignore disposing flag here
        IsConfirmed ??= true;
        IsClosed = true;
    }

    public Task Commit(CancellationToken cancellationToken = default)
    {
        Close(true);
        return Task.CompletedTask;
    }

    public Task Rollback()
    {
        Close(false);
        return Task.CompletedTask;
    }

    public void Close(bool isConfirmed)
    {
        if (IsClosed)
            return;

        if (isConfirmed)
            Operation.LoggedAt = CommandContext.Commander.Hub.Clocks.SystemClock.Now;
        IsConfirmed = isConfirmed;
        IsClosed = true;
        IsUsed = CommandContext.Items.Replace<IOperationScope?>(null, this);
    }
}
