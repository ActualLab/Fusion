using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;

namespace ActualLab.Rpc;

public static class RpcDefaults
{
    private static readonly Lock StaticLock = LockFactory.Create();

    public static RpcMode Mode {
        get;
        set {
            if (value is not (RpcMode.Client or RpcMode.Server))
                throw new ArgumentOutOfRangeException(nameof(value), value, null);

            lock (StaticLock)
                field = value;
        }
    } = OSInfo.IsAnyClient ? RpcMode.Client : RpcMode.Server;

    public static Symbol ApiScope { get; set; } = "Api";
    public static Symbol BackendScope { get; set; } = "Backend";
    public static Version ApiVersion { get; set; } = new(1, 0);
    public static Version BackendVersion { get; set; } = new(1, 0);

    [field: AllowNull, MaybeNull]
    public static VersionSet ApiPeerVersions {
        get {
            if (field?[ApiScope] != ApiVersion)
                lock (StaticLock)
                    if (field?[ApiScope] != ApiVersion)
                        field = new(ApiScope, ApiVersion);
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static VersionSet BackendPeerVersions {
        get {
            if (field?[BackendScope] != BackendVersion)
                lock (StaticLock)
                    if (field?[BackendScope] != BackendVersion)
                        field = new(BackendScope, BackendVersion);
            return field;
        }
    }
}
