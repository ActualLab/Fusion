using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Represents an inbound RPC call that failed because its call type ID does not match the expected type.
/// </summary>
public sealed class RpcInboundInvalidCallTypeCall<TResult>(RpcInboundContext context, byte expectedCallTypeId, byte actualCallTypeId)
    : RpcInboundCall<TResult>(context), IRpcInboundNotFoundCall
{
    public override string DebugTypeName => "<- [invalid call type]";

    public Task InvokeImpl()
    {
        var message = Context.Message;
        var (service, method) = message.MethodRef.GetServiceAndMethodName();
        var expected = RpcCallTypes.GetDescription(expectedCallTypeId);
        var actual = RpcCallTypes.GetDescription(actualCallTypeId);
        return Task.FromException<TResult>(Errors.InvalidCallTypeId(service, method, expected, actual));
    }
}
