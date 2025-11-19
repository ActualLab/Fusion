using ActualLab.Fusion.Rpc;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Server.Rpc;

public static class RpcOptionsExt
{
    extension(RpcOptionDefaults optionDefaults)
    {
        public RpcOptionDefaults ApplyFusionServerOverrides()
        {
            optionDefaults.ApplyFusionOverrides();
            RpcPeerOptions.Default = RpcPeerOptions.Default.WithFusionServerOverrides();
            return optionDefaults;
        }
    }
}
