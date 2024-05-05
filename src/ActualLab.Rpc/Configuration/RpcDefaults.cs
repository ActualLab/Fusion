using System.Diagnostics;
using ActualLab.OS;

namespace ActualLab.Rpc;

public static class RpcDefaults
{
    private static readonly object Lock = new();
    private static VersionSet? _apiPeerVersions;
    private static VersionSet? _backendPeerVersions;
    private static RpcMode _mode;

    public static bool DebugMode { get; set; } = Debugger.IsAttached;

    public static RpcMode Mode {
        get => _mode;
        set {
            if (value is not (RpcMode.Client or RpcMode.Server))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            lock (Lock) {
                _mode = value;
                // Disabled for now due to possible perf. issues
#if false
                var isServer = Mode is RpcMode.Server;
                WebSocketWriteDelayFactory = isServer
                    ? null
                    : TaskExt.YieldDelay;
#endif
            }
        }
    }

    public static Func<Task>? WebSocketWriteDelayFactory { get; set; }
    public static Symbol ApiScope { get; set; } = "Api";
    public static Symbol BackendScope { get; set; } = "Backend";
    public static Version ApiVersion { get; set; } = new(1, 0);
    public static Version BackendVersion { get; set; } = new(1, 0);

    public static VersionSet ApiPeerVersions {
        get {
            if (_apiPeerVersions?[ApiScope] != ApiVersion)
                lock (Lock)
                    if (_apiPeerVersions?[ApiScope] != ApiVersion)
                        _apiPeerVersions = new(ApiScope, ApiVersion);
            return _apiPeerVersions;
        }
    }

    public static VersionSet BackendPeerVersions {
        get {
            if (_backendPeerVersions?[BackendScope] != BackendVersion)
                lock (Lock)
                    if (_backendPeerVersions?[BackendScope] != BackendVersion)
                        _backendPeerVersions = new(BackendScope, BackendVersion);
            return _backendPeerVersions;
        }
    }

    static RpcDefaults()
        => Mode = OSInfo.IsAnyClient ? RpcMode.Client : RpcMode.Server;
}
