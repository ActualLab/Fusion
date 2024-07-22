namespace ActualLab.Rpc;

public partial record RpcPeerRef
{
    private static RpcPeerRef? _remote;
    private static RpcPeerRef? _loopback;
    private static RpcPeerRef? _local;
    private static RpcPeerRef? _backendRemote;
    private static RpcPeerRef? _backendLoopback;
    private static RpcPeerRef? _backendLocal;

    public const string DefaultKey = "default";
    public const string LoopbackKeyPrefix = "loopback:";
    public const string LocalKeyPrefix = "local:";

    public static RpcPeerRef Default { get; set; } = GetDefaultClientPeerRef();
    public static RpcPeerRef Loopback { get; set; } = GetDefaultClientPeerRef(RpcPeerConnectionKind.Loopback, true);
    public static RpcPeerRef Local { get; set; } = GetDefaultClientPeerRef(RpcPeerConnectionKind.Local, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcPeerRef GetDefaultClientPeerRef(bool isBackend = false)
        => GetDefaultClientPeerRef(RpcPeerConnectionKind.Remote, isBackend);

    public static RpcPeerRef GetDefaultClientPeerRef(RpcPeerConnectionKind kind, bool isBackend = false)
        => (kind, isBackend) switch {
            (RpcPeerConnectionKind.Remote, false) => _remote ??= NewClient(DefaultKey),
            (RpcPeerConnectionKind.Loopback, false) => _loopback ??= NewClient(LoopbackKeyPrefix + DefaultKey),
            (RpcPeerConnectionKind.Local, false) => _local ??= NewClient(LocalKeyPrefix + DefaultKey),
            (RpcPeerConnectionKind.Remote, true) => _backendRemote ??= NewClient(DefaultKey, true),
            (RpcPeerConnectionKind.Loopback, true) => _backendLoopback ??= NewClient(LoopbackKeyPrefix + DefaultKey, true),
            (RpcPeerConnectionKind.Local, true) => _backendLocal ??= NewClient(LocalKeyPrefix + DefaultKey, true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
