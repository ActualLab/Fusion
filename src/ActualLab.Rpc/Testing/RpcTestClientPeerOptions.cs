namespace ActualLab.Rpc.Testing;

public class RpcTestClientPeerOptions : RpcPeerOptions
{
    public static new RpcTestClientPeerOptions Default { get; set; } = new();

    public override TimeSpan GetServerPeerCloseTimeout(RpcServerPeer peer)
        => TimeSpan.FromSeconds(10);
}
