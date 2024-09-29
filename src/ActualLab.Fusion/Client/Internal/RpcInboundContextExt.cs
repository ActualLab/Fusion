using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

public static class RpcInboundContextExt
{
    public static string? GetResultVersion(this RpcInboundContext? context)
        => context?.Message.Headers.TryGet(WellKnownRpcHeaders.Version);
}
