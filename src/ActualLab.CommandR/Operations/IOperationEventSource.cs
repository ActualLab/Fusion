namespace ActualLab.CommandR.Operations;

/// <summary>
/// Defines the contract for objects that can produce an <see cref="OperationEvent"/>.
/// </summary>
public interface IOperationEventSource
{
    public OperationEvent ToOperationEvent(IServiceProvider services);
}
