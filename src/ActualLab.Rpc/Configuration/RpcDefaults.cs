using ActualLab.OS;

namespace ActualLab.Rpc;

public static class RpcDefaults
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static volatile RpcMode _mode;
    private static volatile VersionSet? _backendPeerVersions;
    private static volatile VersionSet? _apiPeerVersions;

    public static RpcMode Mode {
        get => _mode;
        set {
            if (value is not (RpcMode.Client or RpcMode.Server))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);

            lock (StaticLock) {
                _mode = value;
                UseCallValidator = value != RpcMode.Client;
            }
        }
    }

    public static bool UseCallValidator { get; set; }
    public static string ApiScope { get; set; } = "Api";
    public static string BackendScope { get; set; } = "Backend";
    public static Version ApiVersion { get; set; } = new(1, 0);
    public static Version BackendVersion { get; set; } = new(1, 0);

    public static VersionSet ApiPeerVersions {
        get {
            if (_apiPeerVersions?[ApiScope] != ApiVersion)
                lock (StaticLock)
                    if (_apiPeerVersions?[ApiScope] != ApiVersion)
                        _apiPeerVersions = new(ApiScope, ApiVersion);
            return _apiPeerVersions;
        }
    }

    public static VersionSet BackendPeerVersions {
        get {
            if (_backendPeerVersions?[BackendScope] != BackendVersion)
                lock (StaticLock)
                    if (_backendPeerVersions?[BackendScope] != BackendVersion)
                        _backendPeerVersions = new(BackendScope, BackendVersion);
            return _backendPeerVersions;
        }
    }

    static RpcDefaults()
        // This assignment has to run at last
        => Mode = OSInfo.IsAnyClient ? RpcMode.Client : RpcMode.Server;

    public static VersionSet GetVersions(bool isBackend)
        => isBackend ? BackendPeerVersions : ApiPeerVersions;
}
