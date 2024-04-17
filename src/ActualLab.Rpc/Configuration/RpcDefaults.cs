namespace ActualLab.Rpc;

public static class RpcDefaults
{
    private static VersionSet? _apiPeerVersions;
    private static VersionSet? _backendPeerVersions;

    public static Symbol ApiScope { get; set; } = "Api";
    public static Symbol BackendScope { get; set; } = "Backend";
    public static Version ApiVersion { get; set; } = new(1, 0);
    public static Version BackendVersion { get; set; } = new(1, 0);

    public static VersionSet ApiPeerVersions {
        get {
            var versions = _apiPeerVersions;
            if (versions == null || versions[ApiScope] != ApiVersion)
                _apiPeerVersions = versions = new(ApiScope, ApiVersion);
            return versions;
        }
    }

    public static VersionSet BackendPeerVersions {
        get {
            var versions = _backendPeerVersions;
            if (versions == null || versions[BackendScope] != BackendVersion)
                _backendPeerVersions = versions = new(BackendScope, BackendVersion);
            return versions;
        }
    }
}
