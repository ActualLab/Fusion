using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcInboundNotFoundCall<TResult>(RpcInboundContext context)
    : RpcInboundCall<TResult>(context)
{
    public override string DebugTypeName => "<- [not found]";

    protected internal override Task InvokeServer()
    {
        var message = Context.Message;
        var (service, method) = message.MethodRef.GetServiceAndMethodName();
        return Task.FromException<TResult>(Errors.EndpointNotFound(service, method));
    }
}
