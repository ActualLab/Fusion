using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcCallHandler(RpcMethodDef methodDef) : IHasServices
{
    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());
    protected bool IsInitialized;

    IServiceProvider IHasServices.Services => Services;
    public readonly RpcMethodDef MethodDef = methodDef;
    public readonly IServiceProvider Services = methodDef.Hub.Services;
    public readonly RpcHub Hub = methodDef.Hub;
}
