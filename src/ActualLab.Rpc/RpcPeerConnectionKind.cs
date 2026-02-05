namespace ActualLab.Rpc;

/// <summary>
/// Defines the kind of connection used by an RPC peer (remote, loopback, local, or none).
/// </summary>
public enum RpcPeerConnectionKind
{
    None = 0, // Used only as RpcPeerRef.ConnectionKind, RpcPeer.ConnectionKind maps this value to Remote
    Remote = 1,
    Loopback = 10,
    Local = 100,
}

/// <summary>
/// Extension methods for <see cref="RpcPeerConnectionKind"/>.
/// </summary>
public static class RpcPeerConnectionKindExt
{
    public static string Format(this RpcPeerConnectionKind source)
        => source switch {
            RpcPeerConnectionKind.None => "none",
            RpcPeerConnectionKind.Remote => "rpc",
            RpcPeerConnectionKind.Loopback => "loopback",
            RpcPeerConnectionKind.Local => "local",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };

    public static bool TryParse(ReadOnlySpan<char> source, out RpcPeerConnectionKind result)
    {
        switch (source) {
            case "none":
                result = RpcPeerConnectionKind.None;
                return true;
            case "rpc":
                result = RpcPeerConnectionKind.Remote;
                return true;
            case "loopback":
                result = RpcPeerConnectionKind.Loopback;
                return true;
            case "local":
                result = RpcPeerConnectionKind.Local;
                return true;
            default:
                result = default;
                return false;
        }
    }
}
