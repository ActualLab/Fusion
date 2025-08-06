using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public partial class RpcPeerRef
{
    private static RpcPeerRef? _remote;
    private static RpcPeerRef? _loopback;
    private static RpcPeerRef? _local;
    private static RpcPeerRef? _none;
    private static RpcPeerRef? _backendRemote;
    private static RpcPeerRef? _backendLoopback;
    private static RpcPeerRef? _backendLocal;
    private static RpcPeerRef? _backendNone;

    public const string DefaultData = "default";

    public static RpcPeerRef Default { get; set; } = GetDefaultPeerRef();
    public static RpcPeerRef Loopback { get; set; } = GetDefaultPeerRef(RpcPeerConnectionKind.Loopback, true);
    public static RpcPeerRef Local { get; set; } = GetDefaultPeerRef(RpcPeerConnectionKind.Local, true);
    public static RpcPeerRef None { get; set; } = GetDefaultPeerRef(RpcPeerConnectionKind.None, true);

    public static Func<string, ParsedRpcPeerRef> Parser { get; set; } = ParsedRpcPeerRef.Parse;

    public static RpcPeerRef NewServer(
        string data,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => NewServer(data, "", isBackend, connectionKind);

    public static RpcPeerRef NewServer(
        string data,
        string serializationFormat,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => new(new ParsedRpcPeerRef() {
            IsServer = true,
            IsBackend = isBackend,
            SerializationFormat = serializationFormat,
            Data = data,
        });

    public static RpcPeerRef NewClient(
        string data,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => NewClient(data, "", isBackend, connectionKind);

    public static RpcPeerRef NewClient(
        string data,
        string serializationFormat,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => new(new ParsedRpcPeerRef() {
            IsBackend = isBackend,
            SerializationFormat = serializationFormat,
            Data = data,
        });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcPeerRef GetDefaultPeerRef(bool isBackend = false)
        => GetDefaultPeerRef(RpcPeerConnectionKind.Remote, isBackend);

    public static RpcPeerRef GetDefaultPeerRef(RpcPeerConnectionKind kind, bool isBackend = false)
        => (kind, isBackend) switch {
            (RpcPeerConnectionKind.Remote, false) => _remote ??= NewClient(DefaultData),
            (RpcPeerConnectionKind.Loopback, false) => _loopback ??= NewClient(DefaultData, false, RpcPeerConnectionKind.Loopback),
            (RpcPeerConnectionKind.Local, false) => _local ??= NewClient(DefaultData, false, RpcPeerConnectionKind.Local),
            (RpcPeerConnectionKind.None, false) => _none ??= NewClient(DefaultData, false, RpcPeerConnectionKind.None),
            (RpcPeerConnectionKind.Remote, true) => _backendRemote ??= NewClient(DefaultData, true),
            (RpcPeerConnectionKind.Loopback, true) => _backendLoopback ??= NewClient(DefaultData, true, RpcPeerConnectionKind.Loopback),
            (RpcPeerConnectionKind.Local, true) => _backendLocal ??= NewClient(DefaultData, true, RpcPeerConnectionKind.Local),
            (RpcPeerConnectionKind.None, true) => _backendNone ??= NewClient(DefaultData, true, RpcPeerConnectionKind.None),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
