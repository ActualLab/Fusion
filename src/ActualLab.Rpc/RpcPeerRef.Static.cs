namespace ActualLab.Rpc;

public partial record RpcPeerRef
{
    private static RpcPeerRef? _remote;
    private static RpcPeerRef? _localChannel;
    private static RpcPeerRef? _localCall;
    private static RpcPeerRef? _backendRemote;
    private static RpcPeerRef? _backendLocalChannel;
    private static RpcPeerRef? _backendLocalCall;

    public const string LocalChannelPrefix = "local:";
    public const string LocalCallPrefix = "call:";

    public static RpcPeerRef Default { get; set; } = GetDefaultClientPeerRef();
    public static RpcPeerRef LocalCall { get; set; } = GetDefaultClientPeerRef(RpcPeerConnectionKind.LocalCall);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RpcPeerRef GetDefaultClientPeerRef(bool isBackend = false)
        => GetDefaultClientPeerRef(RpcPeerConnectionKind.Remote, isBackend);

    public static RpcPeerRef GetDefaultClientPeerRef(RpcPeerConnectionKind kind, bool isBackend = false)
        => (kind, isBackend) switch {
            (RpcPeerConnectionKind.Remote, false) => _remote ??= NewClient("default"),
            (RpcPeerConnectionKind.LocalChannel, false) => _localChannel ??= NewClient(LocalChannelPrefix + "."),
            (RpcPeerConnectionKind.LocalCall, false) => _localCall ??= NewClient(LocalCallPrefix + "."),
            (RpcPeerConnectionKind.Remote, true) => _backendRemote ??= NewClient("default", true),
            (RpcPeerConnectionKind.LocalChannel, true) => _backendLocalChannel ??= NewClient(LocalChannelPrefix + ".", true),
            (RpcPeerConnectionKind.LocalCall, true) => _backendLocalCall ??= NewClient(LocalCallPrefix + ".", true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
