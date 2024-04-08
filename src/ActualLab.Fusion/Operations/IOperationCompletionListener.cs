using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations;

public interface IOperationCompletionListener
{
    Task OnOperationCompleted(Operation operation, CommandContext? commandContext);
}
