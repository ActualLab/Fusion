using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Internal;

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

    public const string DefaultHostId = "default";

    [field: AllowNull, MaybeNull]
    public static RpcPeerRef Default { get => field ??= GetDefaultPeerRef(); set; }
    [field: AllowNull, MaybeNull]
    public static RpcPeerRef Loopback { get => field ??= GetDefaultPeerRef(RpcPeerConnectionKind.Loopback, true); set; }
    [field: AllowNull, MaybeNull]
    public static RpcPeerRef Local { get => field ??= GetDefaultPeerRef(RpcPeerConnectionKind.Local, true); set; }
    [field: AllowNull, MaybeNull]
    public static RpcPeerRef None { get => field ??= GetDefaultPeerRef(RpcPeerConnectionKind.None, true); set; }

    public static RpcPeerRef FromAddress(string address)
        => RpcPeerRefAddress.Parse(address);

    public static RpcPeerRef NewServer(
        string hostInfo,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => NewServer(hostInfo, "", isBackend, connectionKind);

    public static RpcPeerRef NewServer(
        string hostInfo,
        string serializationFormat,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => new RpcPeerRef(isServer: true) {
            IsBackend = isBackend,
            ConnectionKind = connectionKind,
            SerializationFormat = serializationFormat,
            HostInfo = hostInfo,
        }.Initialize();

    public static RpcPeerRef NewClient(
        string hostInfo,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => NewClient(hostInfo, "", isBackend, connectionKind);

    public static RpcPeerRef NewClient(
        string hostInfo,
        string serializationFormat,
        bool isBackend = false,
        RpcPeerConnectionKind connectionKind = RpcPeerConnectionKind.Remote)
        => new RpcPeerRef() {
            IsBackend = isBackend,
            ConnectionKind = connectionKind,
            SerializationFormat = serializationFormat,
            HostInfo = hostInfo,
        }.Initialize();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcPeerRef GetDefaultPeerRef(bool isBackend = false)
        => GetDefaultPeerRef(RpcPeerConnectionKind.Remote, isBackend);

    public static RpcPeerRef GetDefaultPeerRef(RpcPeerConnectionKind kind, bool isBackend = false)
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
