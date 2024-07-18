namespace ActualLab.Rpc;

public partial record RpcPeerRef
{
    private static RpcPeerRef? _remote;
    private static RpcPeerRef? _loopback;
    private static RpcPeerRef? _localCall;
    private static RpcPeerRef? _backendRemote;
    private static RpcPeerRef? _backendLoopback;
    private static RpcPeerRef? _backendLocalCall;

    public const string LoopbackPrefix = "loopback:";
    public const string LocalCallPrefix = "local:";

    public static RpcPeerRef Default { get; set; } = GetDefaultClientPeerRef();
    public static RpcPeerRef Loopback { get; set; } = GetDefaultClientPeerRef(RpcPeerConnectionKind.Loopback, true);
    public static RpcPeerRef LocalCall { get; set; } = GetDefaultClientPeerRef(RpcPeerConnectionKind.LocalCall, true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcPeerRef GetDefaultClientPeerRef(bool isBackend = false)
        => GetDefaultClientPeerRef(RpcPeerConnectionKind.Remote, isBackend);

    public static RpcPeerRef GetDefaultClientPeerRef(RpcPeerConnectionKind kind, bool isBackend = false)
        => (kind, isBackend) switch {
            (RpcPeerConnectionKind.Remote, false) => _remote ??= NewClient("default"),
            (RpcPeerConnectionKind.Loopback, false) => _loopback ??= NewClient(LoopbackPrefix + "."),
            (RpcPeerConnectionKind.LocalCall, false) => _localCall ??= NewClient(LocalCallPrefix + "."),
            (RpcPeerConnectionKind.Remote, true) => _backendRemote ??= NewClient("default", true),
            (RpcPeerConnectionKind.Loopback, true) => _backendLoopback ??= NewClient(LoopbackPrefix + ".", true),
            (RpcPeerConnectionKind.LocalCall, true) => _backendLocalCall ??= NewClient(LocalCallPrefix + ".", true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
