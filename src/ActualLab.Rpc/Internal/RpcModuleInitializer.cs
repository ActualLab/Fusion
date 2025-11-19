using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Internal;

#pragma warning disable CA2255

internal static class RpcModuleInitializer
{
    static RpcModuleInitializer()
    {
        // Access a bunch of types here to ensure JIT generates calls
        // to their methods w/o type initializer check further.
        _ = CpuTimestamp.Now;
        _ = SystemClock.Instance.Now;
        _ = CpuClock.Instance.Now;
        _ = CoarseCpuClock.Instance.Now;
        _ = Timeouts.TickSource;
        _ = RpcInstruments.Meter;
        _ = new RpcOutboundCallSetup();
        _ = new RpcOutboundContext();
        _ = RpcInboundContext.Current;
        _ = RpcCallTypeRegistry.Get(RpcCallTypes.Regular);
    }

#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Touch()
    { }
}
