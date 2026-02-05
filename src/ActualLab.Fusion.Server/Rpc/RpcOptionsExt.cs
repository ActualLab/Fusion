using ActualLab.Fusion.Rpc;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Server.Rpc;

/// <summary>
/// Extension methods for <see cref="RpcOptionDefaults"/> to apply Fusion server overrides.
/// </summary>
public static class RpcOptionsExt
{
    public static RpcOptionDefaults ApplyFusionServerOverrides(this RpcOptionDefaults optionDefaults)
    {
        optionDefaults.ApplyFusionOverrides();
        RpcPeerOptions.Default = RpcPeerOptions.Default.WithFusionServerOverrides();
        return optionDefaults;
    }
}
