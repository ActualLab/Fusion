using ActualLab.Fusion.Client.Internal;
using ActualLab.Rpc;
using Errors = ActualLab.Fusion.Internal.Errors;

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
        if (RemoteExecutionMode != RpcRemoteExecutionMode.Default)
            throw new InvalidOperationException(
                $"Compute method '{FullName}' must use "
                + $"{nameof(RpcRemoteExecutionMode)}.{nameof(RpcRemoteExecutionMode.Default)}.");
        if (service.Mode is RpcServiceMode.Distributed
            && (computedOptions.IsConsolidating || computedOptions.ConsolidationComparerType is not null))
            throw Errors.ConsolidationOnDistributedServiceMethod(service.Type, methodInfo);

        CallType = RpcComputeCallType.Value;
        ComputedOptions = computedOptions;
    }
}
