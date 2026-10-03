using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

/// <summary>
/// Marks an inbound call that invalidates a compute method's result instead of producing it.
/// </summary>
public interface IRpcInboundInvalidateCall;

/// <summary>
/// An inbound compute method call that invalidates its result instead of producing it.
/// It behaves like a Regular call otherwise: the result is the default value, nothing is
/// tracked, and nothing is sent back when the invalidated value changes.
/// </summary>
public sealed class RpcInboundInvalidateCall<TResult>(RpcInboundContext context)
    : RpcInboundCall<TResult>(context), IRpcInboundInvalidateCall;
