using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.CommandR.Rpc;

public static class RpcInboundCallOptionsExt
{
    extension(RpcInboundCallOptions options)
    {
        public RpcInboundCallOptions WithCommanderOverrides()
            => options with { InboundCallServerInvokerDecorator = InboundCallServerInvokerDecorator };

    }

    // Private methods

    private static Func<RpcInboundCall, Task> InboundCallServerInvokerDecorator(RpcMethodDef methodDef, Func<RpcInboundCall, Task> invoker)
    {
        if (methodDef.Kind is not RpcMethodKind.Command)
            return invoker;

        ICommander? commander = null;
        return call => {
            commander ??= call.Hub.Services.Commander();
            var args = call.Arguments!;
            var command = (ICommand)args.Get0Untyped()!;
            var cancellationToken = args.GetCancellationToken(1);
            var context = CommandContext.New(commander, command, isOutermost: true);
            context.Items.KeylessSet(call); // This is the reason we manually create CommandContext here
            var typedCallInvoker = CommanderExt.GetTypedCallInvoker(command.GetResultType());
            return typedCallInvoker.Invoke(commander, context, cancellationToken);
        };
    }
}
