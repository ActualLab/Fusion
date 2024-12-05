using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Interception;

public class RemoteComputeServiceInterceptor : ComputeServiceInterceptor
{
    public new record Options : ComputeServiceInterceptor.Options
    {
        public static new Options Default { get; set; } = new();

        public (LogLevel LogLevel, int MaxDataLength) LogCacheEntryUpdateSettings { get; init; } = (LogLevel.None, 0);
    }

    public readonly RpcServiceDef RpcServiceDef;
    public readonly RpcRoutingInterceptor NonComputeCallInterceptor;
    public readonly RpcNonRoutingInterceptor ComputeCallInterceptor;
    public readonly object? LocalTarget;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RemoteComputeServiceInterceptor(Options settings,
        FusionHub hub,
        RpcRoutingInterceptor nonComputeCallInterceptor,
        RpcNonRoutingInterceptor computeCallInterceptor,
        object? localTarget
        ) : base(settings, hub)
    {
        RpcServiceDef = nonComputeCallInterceptor.ServiceDef;
        if (!ReferenceEquals(RpcServiceDef, computeCallInterceptor.ServiceDef))
            throw new ArgumentOutOfRangeException(nameof(computeCallInterceptor),
                $"{nameof(computeCallInterceptor)}.ServiceDef != {nameof(nonComputeCallInterceptor)}.ServiceDef.");

        NonComputeCallInterceptor = nonComputeCallInterceptor;
        ComputeCallInterceptor = computeCallInterceptor;
        LocalTarget = localTarget;
    }

    public override Func<Invocation, object?>? SelectHandler(in Invocation invocation)
        => GetHandler(invocation) // Compute service method
            ?? NonComputeCallInterceptor.SelectHandler(invocation); // Regular or command service method

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var computeMethodDef = (ComputeMethodDef)methodDef;
        var rpcMethodDef = RpcServiceDef.GetOrFindMethod(initialInvocation.Method);
        if (rpcMethodDef == null) // Proxy is a Distributed service & non-RPC method is called
            return ComputeServiceInterceptor.CreateHandler(new ComputeMethodFunction<TUnwrapped>(computeMethodDef, Hub));

        var function = new RemoteComputeMethodFunction<TUnwrapped>(computeMethodDef, rpcMethodDef, Hub, LocalTarget);
        var ctIndex = computeMethodDef.CancellationTokenIndex;
        return Handler;

        object? Handler(Invocation invocation) {
            var input = new ComputeMethodInput(function, computeMethodDef, invocation);
            var arguments = invocation.Arguments;
            var cancellationToken = ctIndex >= 0 ? arguments.GetCancellationToken(ctIndex) : default;
            try {
                // Inlined:
                // var task = function.InvokeAndStrip(input, ComputeContext.Current, cancellationToken);
                var context = ComputeContext.Current;
                var computed = ComputedRegistry.Instance.Get(input) as Computed<TUnwrapped>; // = input.GetExistingComputed()
                var synchronizer = RemoteComputedSynchronizer.Current;
                var task = !ReferenceEquals(synchronizer, null) && (context.CallOptions & CallOptions.GetExisting) == 0
                    ? UseOrComputeWithSynchronizer()
                    : ComputedImpl.TryUseExisting(computed, context)
                        ? ComputedImpl.StripToTask(computed, context)
                        : function.TryRecompute(input, context, cancellationToken);
                // ReSharper disable once HeapView.BoxingAllocation
                return computeMethodDef.ReturnsValueTask ? new ValueTask<TUnwrapped>(task) : task;

                async Task<TUnwrapped> UseOrComputeWithSynchronizer() {
                    // If we're here, (context.CallOptions & CallOptions.GetExisting) == 0,
                    // which means that only CallOptions.Capture can be used.

                    if (computed == null || !computed.IsConsistent())
                        computed = await function.TryRecomputeForSyncAwaiter(input, cancellationToken).ConfigureAwait(false);
                    var whenSynchronized = synchronizer.WhenSynchronized(computed, cancellationToken);
                    if (!whenSynchronized.IsCompletedSuccessfully()) {
                        await whenSynchronized.ConfigureAwait(false);
                        if (!computed.IsConsistent())
                            computed = await computed.Update(cancellationToken).ConfigureAwait(false);
                    }

                    // Note that until this moment UseNew(...) wasn't called for computed!
                    ComputedImpl.UseNew(computed, context);
                    return computed.Value;
                }
            }
            finally {
                if (cancellationToken.CanBeCanceled)
                    // ComputedInput is stored in ComputeRegistry, so we remove CancellationToken there
                    // to prevent memory leaks + possible unexpected cancellations on .Update calls.
                    arguments.SetCancellationToken(ctIndex, default);
            }

        }
    }

    protected override MethodDef? CreateMethodDef(MethodInfo method,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyType)
        // This interceptor is created on per-service basis, so to reuse the validation cache,
        // we redirect this call to Hub.ComputeServiceInterceptor, which is a singleton.
        => Hub.ComputeServiceInterceptor.GetMethodDef(method, proxyType);

    protected override void ValidateTypeInternal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        // This interceptor is created on per-service basis, so to reuse the validation cache,
        // we redirect this call to Hub.ComputeServiceInterceptor, which is a singleton.
        => Hub.ComputeServiceInterceptor.ValidateType(type);
}
