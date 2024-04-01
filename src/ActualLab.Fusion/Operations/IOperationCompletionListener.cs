namespace ActualLab.Fusion.Operations;

public interface IOperationCompletionListener
{
    bool IsReady();
    Task OnOperationCompleted(Operation operation, CommandContext? commandContext);
}
