using ActualLab.Fusion.Rpc;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CA2255

#if NET8_0_OR_GREATER

internal static class FusionModuleInitializer
{
    static FusionModuleInitializer()
    {
        RpcDefaults.OptionDefaults.ApplyFusionOverrides();
        // Access a bunch of types here to ensure JIT generates calls
        // to their methods w/o type initializer check further.
        ComputedVersion.Next();
    }

    [ModuleInitializer]
    internal static void Touch()
    { }
}

#endif
