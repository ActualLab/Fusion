using ActualLab.CommandR.Rpc;
using ActualLab.Rpc;

namespace ActualLab.CommandR.Internal;

#pragma warning disable CA2255

#if NET8_0_OR_GREATER

internal static class CommanderModuleInitializer
{
    static CommanderModuleInitializer()
        => RpcDefaults.OptionDefaults.ApplyCommanderOverrides();

    [ModuleInitializer]
    internal static void Touch()
    { }
}

#endif
