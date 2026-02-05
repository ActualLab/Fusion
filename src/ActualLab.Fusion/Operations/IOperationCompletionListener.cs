using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations;

/// <summary>
/// A listener that is notified when an operation completes, enabling side-effect processing
/// such as invalidation.
/// </summary>
public interface IOperationCompletionListener
{
    public Task OnOperationCompleted(Operation operation, CommandContext? commandContext);
}
