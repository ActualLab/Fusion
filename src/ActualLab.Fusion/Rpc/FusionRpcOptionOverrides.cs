using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

public static class FusionRpcOptionOverrides
{
    public static RpcRegistryOptions DefaultRegistryOptions { get; set; }
        = new() {
            ServiceDefFactory = ServiceDefFactory,
            MethodDefFactory = MethodDefFactory
        };
    public static RpcOutboundCallOptions DefaultOutboundCallOptions { get; set; }
        = new() { RouterFactory = RouterFactory };

    // Private methods

    private static RpcServiceDef ServiceDefFactory(RpcHub hub, RpcServiceBuilder service)
        => typeof(IComputeService).IsAssignableFrom(service.Type)
            ? new RpcComputeServiceDef(hub, service)
            : new RpcServiceDef(hub, service);

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume RPC-related code is fully preserved")]
    private static RpcMethodDef MethodDefFactory(RpcServiceDef serviceDef, MethodInfo methodInfo)
    {
        if (serviceDef is not RpcComputeServiceDef computeServiceDef)
            return new RpcMethodDef(serviceDef, methodInfo);

        var computedOptions = computeServiceDef.ComputedOptionsProvider.GetComputedOptions(serviceDef.Type, methodInfo);
        return computedOptions is not null
            ? new RpcComputeMethodDef(computeServiceDef, methodInfo, computedOptions)
            : new RpcMethodDef(serviceDef, methodInfo);
    }

    private static Func<ArgumentList, RpcPeerRef> RouterFactory(RpcMethodDef methodDef)
        => methodDef.Kind is RpcMethodKind.Command
            ? static args => Invalidation.IsActive ? RpcPeerRef.Local : RpcPeerRef.Default
            : static args => RpcPeerRef.Default;
}
