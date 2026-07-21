using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public partial class RpcRef
{
    private static RpcRef? _remote;
    private static RpcRef? _loopback;
    private static RpcRef? _local;
    private static RpcRef? _none;
    private static RpcRef? _backendRemote;
    private static RpcRef? _backendLoopback;
    private static RpcRef? _backendLocal;
    private static RpcRef? _backendNone;

    public const string DefaultHostId = "default";

    public static RpcRef Default { get => field ??= GetDefaultRef(); set; }
    public static RpcRef DefaultBackend { get => field ??= GetDefaultRef(isBackend: true); set; }
    public static RpcRef Loopback { get => field ??= GetDefaultRef(RpcPeerConnectionKind.Loopback, true); set; }
    public static RpcRef Local { get => field ??= GetDefaultRef(RpcPeerConnectionKind.Local, true); set; }
    public static RpcRef None { get => field ??= GetDefaultRef(RpcPeerConnectionKind.None, true); set; }

    public static RpcRef FromAddress(string address)
        => RpcRefAddress.Parse(address);

    public static RpcRef NewServer(
        string hostInfo,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => NewServer(hostInfo, "", isBackend, connectionKind);

    public static RpcRef NewServer(
        string hostInfo,
        string serializationFormat,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => new RpcRef() {
            IsServer = true,
            IsBackend = isBackend,
            ConnectionKind = connectionKind,
            SerializationFormat = serializationFormat,
            HostInfo = hostInfo,
        }.Initialize();

    public static RpcRef NewClient(
        string hostInfo,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => NewClient(hostInfo, "", isBackend, connectionKind);

    public static RpcRef NewClient(
        string hostInfo,
        string serializationFormat,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => new RpcRef() {
            IsBackend = isBackend,
            ConnectionKind = connectionKind,
            SerializationFormat = serializationFormat,
            HostInfo = hostInfo,
        }.Initialize();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcRef GetDefaultRef(bool isBackend = false)
        => GetDefaultRef(RpcPeerConnectionKind.Remote, isBackend);

    public static RpcRef GetDefaultRef(RpcPeerConnectionKind kind, bool isBackend = false)
        => (kind, isBackend) switch {
            (RpcPeerConnectionKind.Remote, false) => _remote ??= NewClient(DefaultHostId),
            (RpcPeerConnectionKind.Loopback, false) => _loopback ??= NewClient(DefaultHostId, false, RpcPeerConnectionKind.Loopback),
            (RpcPeerConnectionKind.Local, false) => _local ??= NewClient(DefaultHostId, false, RpcPeerConnectionKind.Local),
            (RpcPeerConnectionKind.None, false) => _none ??= NewClient(DefaultHostId, false, RpcPeerConnectionKind.None),
            (RpcPeerConnectionKind.Remote, true) => _backendRemote ??= NewClient(DefaultHostId, true),
            (RpcPeerConnectionKind.Loopback, true) => _backendLoopback ??= NewClient(DefaultHostId, true, RpcPeerConnectionKind.Loopback),
            (RpcPeerConnectionKind.Local, true) => _backendLocal ??= NewClient(DefaultHostId, true, RpcPeerConnectionKind.Local),
            (RpcPeerConnectionKind.None, true) => _backendNone ??= NewClient(DefaultHostId, true, RpcPeerConnectionKind.None),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
