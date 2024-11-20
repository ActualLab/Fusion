using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations;

public interface IOperationCompletionListener
{
    public Task OnOperationCompleted(Operation operation, CommandContext? commandContext);
}
