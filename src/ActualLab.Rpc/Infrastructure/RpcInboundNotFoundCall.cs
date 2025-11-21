using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcInboundNotFoundCall<TResult>(RpcInboundContext context)
    : RpcInboundCall<TResult>(context), IRpcInboundNotFoundCall
{
    public override string DebugTypeName => "<- [not found]";

    public Task InvokeImpl()
    {
        var message = Context.Message;
        var (service, method) = message.MethodRef.GetServiceAndMethodName();
        return Task.FromException<TResult>(Errors.EndpointNotFound(service, method));
    }
}
