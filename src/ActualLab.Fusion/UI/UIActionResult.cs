namespace ActualLab.Fusion.UI;

public interface IUIActionResult : IResult
{
    public long ActionId { get; }
    public UIAction UntypedAction { get; }
    public ICommand Command { get; }
    public Moment StartedAt { get; }
    public Moment CompletedAt { get; }
    public TimeSpan Duration { get; }
    public CancellationToken CancellationToken { get; }
}

public class UIActionResult<TResult> : ResultBox<TResult>, IUIActionResult
{
    public UIAction<TResult> Action { get; }
    public Moment CompletedAt { get; }

    // Computed properties
    public long ActionId => Action.ActionId;
    public UIAction UntypedAction => Action;
    public ICommand Command => Action.Command;
    public Moment StartedAt => Action.StartedAt;
    public TimeSpan Duration => CompletedAt - StartedAt;
    public CancellationToken CancellationToken => Action.CancellationToken;

    public UIActionResult(UIAction<TResult> action, Result<TResult> result, Moment completedAt)
        : base(result)
    {
        Action = action;
        CompletedAt = completedAt;
    }

    // Conversion

    public override string ToString()
        => $"{GetType().GetName()}(#{ActionId}: {AsResult()}, Duration = {Duration.ToShortString()})";
}
