namespace ActualLab.Fusion.Operations;

public interface IOperationCompletionListener
{
    bool IsReady();
    Task OnOperationCompleted(IOperation operation, CommandContext? commandContext);
}
