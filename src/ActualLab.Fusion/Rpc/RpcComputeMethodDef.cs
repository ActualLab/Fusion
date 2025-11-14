using ActualLab.Fusion.Client.Internal;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

public class RpcComputeMethodDef : RpcMethodDef
{
    public readonly ComputedOptions ComputedOptions;

    public RpcComputeMethodDef(RpcServiceDef service, MethodInfo methodInfo, ComputedOptions computedOptions)
        : base(service, methodInfo)
    {
        CallTypeId = RpcComputeCallType.Id;
        ComputedOptions = computedOptions;
    }
}
