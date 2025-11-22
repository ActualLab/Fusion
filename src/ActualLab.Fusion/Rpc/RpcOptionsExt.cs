using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

public static class RpcOptionsExt
{
    public static RpcOptionDefaults ApplyFusionOverrides(this RpcOptionDefaults optionDefaults)
    {
        RpcRegistryOptions.Default = RpcRegistryOptions.Default.WithFusionOverrides();
        return optionDefaults;
    }
}
