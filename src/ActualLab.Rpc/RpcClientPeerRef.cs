namespace ActualLab.Rpc;

public class RpcClientPeerRef : RpcPeerRef
{
    public override bool IsServer => false;

    // Equality of RpcClientPeerRef is reference-based
}
