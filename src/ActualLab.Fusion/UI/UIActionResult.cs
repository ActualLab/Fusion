using ActualLab.Conversion;

namespace ActualLab.Fusion.UI;

/// <summary>
/// The result of a completed <see cref="UIAction"/>, including timing information.
/// </summary>
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

/// <summary>
/// A strongly-typed <see cref="IUIActionResult"/> carrying the result of a <see cref="UIAction{TResult}"/>.
/// </summary>
public class UIActionResult<T>(UIAction<T> action, Result<T> result, Moment completedAt)
    : IResult<T>, IUIActionResult
{
    public UIAction<T> Action { get; } = action;
    public Result<T> Result { get; } = result;
    public Moment CompletedAt { get; } = completedAt;

    // Computed properties
    public long ActionId => Action.ActionId;
    public UIAction UntypedAction => Action;
    public ICommand Command => Action.Command;
    public Moment StartedAt => Action.StartedAt;
    public TimeSpan Duration => CompletedAt - StartedAt;
    public CancellationToken CancellationToken => Action.CancellationToken;

    // IResult<T> implementation
    public T Value => Result.Value;
    object? IResult.Value => (Result as IResult).Value;
    public T? ValueOrDefault => Result.ValueOrDefault;
    public Exception? Error => Result.Error;
    public bool HasValue => Result.HasValue;
    public bool HasError => Result.HasError;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out T value, out Exception? error)
        => Result.Deconstruct(out value, out error);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IResult.Deconstruct(out object? untypedValue, out Exception? error)
        => ((IResult)Result).Deconstruct(out untypedValue, out error);
    T IConvertibleTo<T>.Convert() => Result.Value;
    public object? GetUntypedValueOrErrorBox() => Result.GetUntypedValueOrErrorBox();

    public override string ToString()
        => $"{GetType().GetName()}(#{ActionId}: {Result}, Duration = {Duration.ToShortString()})";
}
