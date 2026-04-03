using ActualLab.CommandR.Trimming;
using ActualLab.Interception.Trimming;
using ActualLab.Trimming;

namespace ActualLab.CommandR.Internal;

#pragma warning disable CA2255

/// <summary>
/// Module initializer that pre-warms JIT compilation for frequently used RPC types.
/// </summary>
internal static class CommanderModuleInitializer
{
    static CommanderModuleInitializer()
    {
        if (CodeKeeper.AlwaysFalse)
            ProxyCodeKeeper.Extension = new CommanderProxyCodeKeeperExtension();
    }

#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Touch()
    { }
}
