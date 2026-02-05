using ActualLab.Fusion.Client.Internal;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

/// <summary>
/// An <see cref="RpcMethodDef"/> for compute methods, carrying <see cref="ComputedOptions"/>
/// and using the Fusion-specific RPC call type.
/// </summary>
public class RpcComputeMethodDef : RpcMethodDef
{
    public readonly ComputedOptions ComputedOptions;

    public RpcComputeMethodDef(RpcServiceDef service, MethodInfo methodInfo, ComputedOptions computedOptions)
        : base(service, methodInfo)
    {
        CallType = RpcComputeCallType.Value;
        ComputedOptions = computedOptions;
    }

    protected override RpcLocalExecutionMode GetDefaultLocalExecutionMode()
        => Service.Mode is RpcServiceMode.Distributed
            ? RpcLocalExecutionMode.ConstrainedEntry
            : RpcLocalExecutionMode.Unconstrained;
}
