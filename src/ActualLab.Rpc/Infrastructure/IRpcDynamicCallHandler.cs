using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public interface IRpcDynamicCallHandler
{
    bool IsValidCall(RpcInboundContext context, ref ArgumentList arguments, ref bool allowPolymorphism);
}
