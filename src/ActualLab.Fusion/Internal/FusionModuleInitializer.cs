using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Rpc;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CA2255

internal static class FusionModuleInitializer
{
    static FusionModuleInitializer()
    {
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
