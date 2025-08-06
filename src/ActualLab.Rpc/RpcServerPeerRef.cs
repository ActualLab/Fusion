namespace ActualLab.Rpc;

#pragma warning disable MA0001, MA0021

public class RpcServerPeerRef : RpcPeerRef
{
    // Equality of RpcServerPeerRef is based on the Address property

    protected bool Equals(RpcServerPeerRef other)
        => Address.Equals(other.Address);

    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj) || (obj is RpcServerPeer other && Equals(other));

    public override int GetHashCode()
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        => Address.GetHashCode();
}
