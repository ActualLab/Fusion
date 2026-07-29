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
    private static VersionSet? _backendPeerVersions;
    private static VersionSet? _apiPeerVersions;

    public static RpcOptionDefaults OptionDefaults { get; } = new();
    public static string ApiScope { get; set; } = "Api";
    public static string BackendScope { get; set; } = "Backend";
    public static Version ApiVersion { get; set; } = new(1, 0);
    public static Version BackendVersion { get; set; } = new(1, 0);

    public static VersionSet ApiPeerVersions {
        get {
            if (_apiPeerVersions is { } value && value[ApiScope] == ApiVersion)
                return value;

            lock (StaticLock) {
                if (_apiPeerVersions is { } newValue && newValue[ApiScope] == ApiVersion)
                    return newValue;

                newValue = new VersionSet(ApiScope, ApiVersion);
                Volatile.Write(ref _apiPeerVersions, newValue);
                return newValue;
            }
        }
    }

    public static VersionSet BackendPeerVersions {
        get {
            if (_backendPeerVersions is { } value && value[BackendScope] == BackendVersion)
                return value;

            lock (StaticLock) {
                if (_backendPeerVersions is { } newValue && newValue[BackendScope] == BackendVersion)
                    return newValue;

                newValue = new VersionSet(BackendScope, BackendVersion);
                Volatile.Write(ref _backendPeerVersions, newValue);
                return newValue;
            }
        }
    }

    public static VersionSet GetVersions(bool isBackend)
        => isBackend ? BackendPeerVersions : ApiPeerVersions;
}
