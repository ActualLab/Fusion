using ActualLab.CommandR.Rpc;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Rpc;

public static class RpcOptionsExt
{
    extension(RpcOptionDefaults optionDefaults)
    {
        public RpcOptionDefaults ApplyFusionOverrides()
        {
            optionDefaults.ApplyCommanderOverrides();
            RpcRegistryOptions.Default = RpcRegistryOptions.Default.WithFusionOverrides();
            RpcOutboundCallOptions.Default = RpcOutboundCallOptions.Default.WithFusionOverrides();
            return optionDefaults;
        }
    }
}
