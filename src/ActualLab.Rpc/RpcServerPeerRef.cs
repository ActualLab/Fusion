namespace ActualLab.Rpc;

public class RpcServerPeerRef() : RpcPeerRef(true)
{
    protected int HashCode {
        get {
            if (field != 0)
                return field;

            field = Address.GetOrdinalHashCode();
            if (field == 0)
                field = -1;
            return field;
        }
    }

    // The equality of RpcServerPeerRef is based solely on the Address property,
    // so two RpcServerPeerRef instances with the same Address are considered equal.
    // This is necessary to make sure an RPC client can reconnect to exactly the same peer rather than a new one.
    //
    // See RpcWebSocketServer.Invoke and RpcWebSocketServerPeerRefFactory implementations,
    // they use the 'clientId' parameter to construct a new RpcServerPeerRef on each WebSocket connection.

#pragma warning disable MA0001
    protected bool Equals(RpcServerPeerRef other)
        => HashCode == other.HashCode && Address.Equals(other.Address);

    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj) || (obj is RpcServerPeer other && Equals(other));

    public override int GetHashCode()
        => HashCode;
#pragma warning restore MA0001
}
