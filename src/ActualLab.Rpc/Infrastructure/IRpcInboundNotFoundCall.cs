namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Implemented by inbound call types that handle "method not found" scenarios.
/// </summary>
public interface IRpcInboundNotFoundCall
{
    public Task InvokeImpl();
}
