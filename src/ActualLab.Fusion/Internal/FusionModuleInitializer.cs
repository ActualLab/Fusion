using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Rpc;
using ActualLab.Fusion.Trimming;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc;
using ActualLab.Trimming;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CA2255

/// <summary>
/// Module initializer that registers Fusion-specific RPC defaults and triggers
/// early JIT compilation of frequently used types.
/// </summary>
internal static class FusionModuleInitializer
{
    static FusionModuleInitializer()
    {
        if (CodeKeeper.AlwaysFalse)
            ProxyCodeKeeper.Extension = new FusionProxyCodeKeeperExtension();

        _ = RpcComputeCallType.Value;
        RpcDefaults.OptionDefaults.ApplyFusionOverrides();
        // Access a bunch of types here to ensure JIT generates calls
        // to their methods w/o type initializer check further.
        ComputedVersion.Next();
    }

#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Touch()
    { }
}
