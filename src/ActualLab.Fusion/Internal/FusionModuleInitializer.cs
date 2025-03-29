using ActualLab.Rpc;
using ActualLab.Rpc.Diagnostics;

namespace ActualLab.Fusion.Internal;

#pragma warning disable CA2255

#if NET8_0_OR_GREATER

internal static class FusionModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // We access a bunch of types here to ensure JIT will generate calls
        // to their methods w/o type initializer check further.
        _ = CpuTimestamp.Now;
        _ = CoarseCpuClock.Instance.Now;
        _ = Timeouts.TickSource;
        _ = RpcInstruments.Meter;
        _ = RpcCallTypeRegistry.Get(RpcCallTypes.Regular);
        ComputedVersion.Next();
    }
}

#endif
