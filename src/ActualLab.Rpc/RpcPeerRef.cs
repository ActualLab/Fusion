namespace ActualLab.Rpc;

public record RpcPeerRef(Symbol Key, bool IsServer = false, bool IsBackend = false)
{
    public static RpcPeerRef Default { get; set; } = NewClient("default");

    public static RpcPeerRef NewServer(Symbol key, bool isBackend = false)
        => new(key, true, isBackend);
    public static RpcPeerRef NewClient(Symbol key, bool isBackend = false)
        => new(key, false, isBackend);

    public override string ToString()
        => $"{(IsBackend ? "backend-" : "")}{(IsServer ? "server" : "client")}:{Key}";

    // Operators

    public static implicit operator RpcPeerRef(RpcPeer peer) => peer.Ref;
}
