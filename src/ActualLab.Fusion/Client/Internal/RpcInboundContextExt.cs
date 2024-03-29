using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

public static class RpcInboundContextExt
{
    public static string? GetResultVersion(this RpcInboundContext? context)
    {
        if (context == null)
            return null;

        return context.Message.Headers.TryGet(FusionRpcHeaders.Version.Name, out var versionHeader)
            ? versionHeader.Value
            : null;
    }
}
