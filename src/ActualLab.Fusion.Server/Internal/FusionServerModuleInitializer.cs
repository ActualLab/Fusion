using ActualLab.Fusion.Server.Rpc;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Server.Internal;

#pragma warning disable CA2255

internal static class FusionServerModuleInitializer
{
    static FusionServerModuleInitializer()
        => RpcDefaults.OptionDefaults.ApplyFusionServerOverrides();

#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Touch()
    { }
}
