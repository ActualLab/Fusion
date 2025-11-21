using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    public Func<IRpcMiddleware, bool>? MiddlewareFilter { get; protected set; } = null;

    // The delegates and properties below must be initialized in Initialize(),
    // they are supposed to be as efficient as possible (i.e., do less, if possible)
    // taking the values of other properties into account.
    public Func<RpcInboundContext, RpcInboundCall> InboundCallFactory { get; protected set; } = null!;
    public Func<RpcInboundCall, Task> InboundCallInvoker { get; protected set; } = null!;
}
