using ActualLab.Interception.Trimming;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Trimming;
using ActualLab.Trimming;

namespace ActualLab.Rpc.Internal;

#pragma warning disable CA2255

/// <summary>
/// Module initializer that pre-warms JIT compilation for frequently used RPC types.
/// </summary>
internal static class RpcModuleInitializer
{
    static RpcModuleInitializer()
    {
        if (CodeKeeper.AlwaysFalse)
            ProxyCodeKeeper.Extension = new RpcProxyCodeKeeperExtension();

        // Access a bunch of types here to ensure JIT generates calls
        // to their methods w/o type initializer check further.
        _ = SystemClock.Instance.Now;
        _ = CpuClock.Instance.Now;
        _ = CoarseSystemClock.Instance.Now;
        _ = Timeouts.TickSource;
        _ = RpcInstruments.Meter;
        _ = new RpcOutboundCallSetup();
        _ = new RpcOutboundContext();
        _ = RpcInboundContext.Current;
        _ = RpcCallTypes.Get(RpcCallTypes.Regular.Id);
    }

#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Touch()
    { }
}
