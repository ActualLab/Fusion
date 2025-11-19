using ActualLab.Rpc;

namespace ActualLab.CommandR.Rpc;

public static class RpcOptionsExt
{
    extension(RpcOptionDefaults optionDefaults)
    {
        public RpcOptionDefaults ApplyCommanderOverrides()
        {
            RpcInboundCallOptions.Default = RpcInboundCallOptions.Default.WithCommanderOverrides();
            return optionDefaults;
        }
    }
}
