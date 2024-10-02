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

    public static RpcPeerRef Default { get; set; } = GetDefaultPeerRef();
    public static RpcPeerRef Loopback { get; set; } = GetDefaultPeerRef(RpcPeerConnectionKind.Loopback, true);
    public static RpcPeerRef Local { get; set; } = GetDefaultPeerRef(RpcPeerConnectionKind.Local, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Symbol ComposeKey(string prefix, string serializationFormat)
        => $"{prefix}${serializationFormat}";

    public static RpcPeerRef NewServer(string clientId, string serializationFormat, bool isBackend = false)
        => new(ComposeKey(clientId, serializationFormat), true, isBackend);
    public static RpcPeerRef NewServer(Symbol key,  bool isBackend = false)
        => new(key, true, isBackend);

    public static RpcPeerRef NewClient(string clientId, string serializationFormat, bool isBackend = false)
        => new(ComposeKey(clientId, serializationFormat), false, isBackend);
    public static RpcPeerRef NewClient(Symbol key, bool isBackend = false)
        => new(key, false, isBackend);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcPeerRef GetDefaultPeerRef(bool isBackend = false)
        => GetDefaultPeerRef(RpcPeerConnectionKind.Remote, isBackend);

    public static RpcPeerRef GetDefaultPeerRef(RpcPeerConnectionKind kind, bool isBackend = false)
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
