namespace ActualLab.Fusion.UI;

/// <summary>
/// Represents a UI-initiated command execution, tracking its start time and completion.
/// </summary>
public abstract class UIAction(ICommand command, Moment startedAt, CancellationToken cancellationToken)
{
    private static long _nextActionId;

    public long ActionId { get; } = Interlocked.Increment(ref _nextActionId);
    public ICommand Command { get; } = command;
    public Moment StartedAt { get; } = startedAt;
    public CancellationToken CancellationToken { get; } = cancellationToken;

    public abstract IUIActionResult? UntypedResult { get; }
    public abstract Task WhenCompleted();

    public override string ToString()
        => $"{GetType().GetName()}(#{ActionId}: {Command}, {UntypedResult?.ToString() ?? "still running"})";
}

/// <summary>
/// A strongly-typed <see cref="UIAction"/> that produces a <see cref="UIActionResult{T}"/>
/// upon completion.
/// </summary>
public class UIAction<TResult> : UIAction
{
    public Task<UIActionResult<TResult>> ResultTask { get; }

    // Computed properties
    public override IUIActionResult? UntypedResult => Result;
    public UIActionResult<TResult>? Result => ResultTask.IsCompleted ? ResultTask.GetAwaiter().GetResult() : null;

    protected UIAction(ICommand<TResult> command, MomentClock clock, CancellationToken cancellationToken)
        : base(command, clock.Now, cancellationToken)
        => ResultTask = null!;

    public UIAction(ICommand<TResult> command, MomentClock clock, Task<TResult> resultTask, CancellationToken cancellationToken)
        : base(command, clock.Now, cancellationToken)
    {
        ResultTask = resultTask.ContinueWith(
            t => {
                var result = t.ToResultSynchronously();
                var completedAt = clock.Now;
                return new UIActionResult<TResult>(this, result, completedAt);
            },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public override Task WhenCompleted()
        => ResultTask;
}
