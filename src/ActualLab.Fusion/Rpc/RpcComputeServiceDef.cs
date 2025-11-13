using ActualLab.Fusion.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

public class RpcComputeServiceDef(RpcHub hub, RpcServiceBuilder service) : RpcServiceDef(hub, service)
{
    public ComputedOptionsProvider ComputedOptionsProvider { get; init; }
        = hub.Services.GetRequiredService<ComputedOptionsProvider>();
}
