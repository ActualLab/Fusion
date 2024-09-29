using ActualLab.OS;

namespace ActualLab.Rpc;

public static class RpcDefaults
{
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif

    private static RpcMode _mode = OSInfo.IsAnyClient ? RpcMode.Client : RpcMode.Server;
    private static VersionSet? _apiPeerVersions;
    private static VersionSet? _backendPeerVersions;

    public static RpcMode Mode {
        get => _mode;
        set {
            if (value is not (RpcMode.Client or RpcMode.Server))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
            lock (Lock)
                _mode = value;
        }
    }

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
}
