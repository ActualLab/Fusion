using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

/// <summary>
/// Extension methods for <see cref="RpcOptionDefaults"/>.
/// </summary>
public static class RpcOptionsExt
{
    public static RpcOptionDefaults ApplyFusionOverrides(this RpcOptionDefaults optionDefaults)
    {
        RpcRegistryOptions.Default = RpcRegistryOptions.Default.WithFusionOverrides();
        return optionDefaults;
    }
}
