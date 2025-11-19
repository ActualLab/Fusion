using ActualLab.CommandR.Rpc;
using ActualLab.Rpc;

namespace ActualLab.CommandR.Internal;

#pragma warning disable CA2255

internal static class CommanderModuleInitializer
{
    static CommanderModuleInitializer()
        => RpcDefaults.OptionDefaults.ApplyCommanderOverrides();

#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Touch()
    { }
}
