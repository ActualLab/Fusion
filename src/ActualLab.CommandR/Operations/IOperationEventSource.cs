namespace ActualLab.CommandR.Operations;

public interface IOperationEventSource
{
    public OperationEvent ToOperationEvent(IServiceProvider services);
}
