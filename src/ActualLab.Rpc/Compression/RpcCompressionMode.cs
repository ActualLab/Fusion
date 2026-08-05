namespace ActualLab.Rpc.Compression;

public enum RpcCompressionMode
{
    None = 0,
    ServerToClient = 1,
    ClientToServer = 2,
    Full = ServerToClient + ClientToServer,
}

public static class RpcCompressionModeExt
{
    public static bool MustCompress(this RpcCompressionMode mode, bool isServer)
    {
        var requiredFlag = isServer
            ? RpcCompressionMode.ServerToClient
            : RpcCompressionMode.ClientToServer;
        return mode.HasFlag(requiredFlag);
    }
}
