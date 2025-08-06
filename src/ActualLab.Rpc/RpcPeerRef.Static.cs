using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public partial class RpcPeerRef
{
    private static RpcPeerRef? _remote;
    private static RpcPeerRef? _loopback;
    private static RpcPeerRef? _local;
    private static RpcPeerRef? _backendRemote;
    private static RpcPeerRef? _backendLoopback;
    private static RpcPeerRef? _backendLocal;

    public const string DefaultClientId = "default";

    public static RpcPeerRef Default { get; set; } = GetDefaultPeerRef();
    public static RpcPeerRef Loopback { get; set; } = GetDefaultPeerRef(RpcPeerConnectionKind.Loopback, true);
    public static RpcPeerRef Local { get; set; } = GetDefaultPeerRef(RpcPeerConnectionKind.Local, true);

    public static Func<string, ParsedRpcPeerRef> Parse { get; set; } = ParsedRpcPeerRef.Parse;

    public static RpcPeerRef NewServer(
        string clientId,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => NewServer(clientId, "", isBackend, connectionKind);

    public static RpcPeerRef NewServer(
        string clientId,
        string serializationFormat,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => new(new ParsedRpcPeerRef() {
            IsServer = true,
            IsBackend = isBackend,
            SerializationFormatKey = serializationFormat,
            Unparsed = clientId,
        });

    public static RpcPeerRef NewClient(
        string clientId,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => NewClient(clientId, "", isBackend, connectionKind);

    public static RpcPeerRef NewClient(
        string clientId,
        string serializationFormat,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => new(new ParsedRpcPeerRef() {
            IsBackend = isBackend,
            SerializationFormatKey = serializationFormat,
            Unparsed = clientId,
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcPeerRef GetDefaultPeerRef(bool isBackend = false)
        => GetDefaultPeerRef(RpcPeerConnectionKind.Remote, isBackend);

    public static RpcPeerRef GetDefaultPeerRef(RpcPeerConnectionKind kind, bool isBackend = false)
        => (kind, isBackend) switch {
            (RpcPeerConnectionKind.Remote, false) => _remote ??= NewClient(DefaultClientId),
            (RpcPeerConnectionKind.Loopback, false) => _loopback ??= NewClient(DefaultClientId, false, RpcPeerConnectionKind.Loopback),
            (RpcPeerConnectionKind.Local, false) => _local ??= NewClient(DefaultClientId, false, RpcPeerConnectionKind.Local),
            (RpcPeerConnectionKind.Remote, true) => _backendRemote ??= NewClient(DefaultClientId, true),
            (RpcPeerConnectionKind.Loopback, true) => _backendLoopback ??= NewClient(DefaultClientId, true, RpcPeerConnectionKind.Loopback),
            (RpcPeerConnectionKind.Local, true) => _backendLocal ??= NewClient(DefaultClientId, true, RpcPeerConnectionKind.Local),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
