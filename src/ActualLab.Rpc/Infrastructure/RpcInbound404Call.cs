using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcInbound404Call<TResult>(RpcInboundContext context, RpcMethodDef methodDef)
    : RpcInboundCall<TResult>(context, methodDef)
{
    public override string DebugTypeName => "<- [not found]";

    protected override Task<TResult> InvokeTarget()
    {
        var message = Context.Message;
        var (service, method) = message.MethodRef.GetServiceAndMethodName();
        return Task.FromException<TResult>(Errors.EndpointNotFound(service, method));
    }
}
