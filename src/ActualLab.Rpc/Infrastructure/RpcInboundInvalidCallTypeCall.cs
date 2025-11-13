using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcInboundInvalidCallTypeCall<TResult>(RpcInboundContext context, byte expectedCallTypeId, byte actualCallTypeId)
    : RpcInboundCall<TResult>(context)
{
    public override string DebugTypeName => "<- [invalid call type]";

    protected internal override Task<TResult> InvokeServer()
    {
        var message = Context.Message;
        var (service, method) = message.MethodRef.GetServiceAndMethodName();
        var expected = RpcCallTypeRegistry.GetDescription(expectedCallTypeId);
        var actual = RpcCallTypeRegistry.GetDescription(actualCallTypeId);
        return Task.FromException<TResult>(Errors.InvalidCallTypeId(service, method, expected, actual));
    }
}
