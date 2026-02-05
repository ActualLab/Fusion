namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Abstract base class for handlers associated with a specific <see cref="RpcMethodDef"/>.
/// </summary>
public abstract class RpcCallHandler(RpcMethodDef methodDef) : IHasServices
{
    protected ILogger Log => field ??= Services.LogFor(GetType());
    protected bool IsInitialized;

    IServiceProvider IHasServices.Services => Services;
    public readonly RpcMethodDef MethodDef = methodDef;
    public readonly IServiceProvider Services = methodDef.Hub.Services;
    public readonly RpcHub Hub = methodDef.Hub;
}
