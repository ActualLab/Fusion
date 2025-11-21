using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

public static class RpcOptionsExt
{
    extension(RpcOptionDefaults optionDefaults)
    {
        public RpcOptionDefaults ApplyFusionOverrides()
        {
            RpcRegistryOptions.Default = RpcRegistryOptions.Default.WithFusionOverrides();
            return optionDefaults;
        }
    }
}
