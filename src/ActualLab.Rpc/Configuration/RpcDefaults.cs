namespace ActualLab.Rpc;

/// <summary>
/// Provides default API and backend scope names, versions, and peer version sets used by the RPC framework.
/// </summary>
public static class RpcDefaults
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static volatile VersionSet? _backendPeerVersions;
    private static volatile VersionSet? _apiPeerVersions;

    public static RpcOptionDefaults OptionDefaults { get; } = new();
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

    public static VersionSet GetVersions(bool isBackend)
        => isBackend ? BackendPeerVersions : ApiPeerVersions;
}
