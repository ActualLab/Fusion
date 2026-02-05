using ActualLab.Fusion.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

/// <summary>
/// An <see cref="RpcServiceDef"/> for <see cref="IComputeService"/> types,
/// providing access to <see cref="Interception.ComputedOptionsProvider"/>.
/// </summary>
public class RpcComputeServiceDef(RpcHub hub, RpcServiceBuilder service) : RpcServiceDef(hub, service)
{
    public ComputedOptionsProvider ComputedOptionsProvider { get; init; }
        = hub.Services.GetRequiredService<ComputedOptionsProvider>();
}
