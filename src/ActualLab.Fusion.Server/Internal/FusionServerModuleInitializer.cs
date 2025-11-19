using ActualLab.Fusion.Server.Rpc;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Server.Internal;

#pragma warning disable CA2255

#if NET8_0_OR_GREATER

internal static class FusionServerModuleInitializer
{
    static FusionServerModuleInitializer()
        => RpcDefaults.OptionDefaults.ApplyFusionServerOverrides();

    [ModuleInitializer]
    internal static void Touch()
    { }
}

#endif
