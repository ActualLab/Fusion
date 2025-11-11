namespace ActualLab.Rpc.Testing;

public class RpcTestClientPeerOptions(IServiceProvider services) : RpcPeerOptions(services)
{
    public override TimeSpan GetServerPeerCloseTimeout(RpcServerPeer peer)
        => TimeSpan.FromSeconds(10);
}
