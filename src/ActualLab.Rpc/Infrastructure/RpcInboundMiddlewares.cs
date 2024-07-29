namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcInboundMiddlewares(IServiceProvider services)
    : RpcMiddlewares<RpcInboundMiddleware>(services)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcInboundMiddlewares? NullIfEmpty()
        => HasInstances ? this : null;

    public async Task OnBeforeCall(RpcInboundCall call)
    {
        foreach (var m in Instances) {
            var task = m.OnBeforeCall(call);
            if (!task.IsCompletedSuccessfully())
                await task.ConfigureAwait(false);
        }
    }

    public async Task OnAfterCall(RpcInboundCall call, Task resultTask)
    {
        foreach (var m in InstancesReversed) {
            var task = m.OnAfterCall(call, resultTask);
            if (!task.IsCompletedSuccessfully())
                await task.ConfigureAwait(false);
        }
    }
}
