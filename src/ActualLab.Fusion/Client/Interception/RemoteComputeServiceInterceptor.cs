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
        var function = rpcMethodDef == null
            ? new ComputeMethodFunction<TUnwrapped>(computeMethodDef, Hub) // No RpcMethodDef -> it's a local call
            : new RemoteComputeMethodFunction<TUnwrapped>(computeMethodDef, rpcMethodDef, Hub, LocalTarget);
        return CreateHandler(function);
    }

    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
        // This interceptor is created on per-service basis, so to reuse the validation cache,
        // we redirect this call to Hub.ComputeServiceInterceptor, which is a singleton.
        => Hub.Interceptor.GetMethodDef(method, proxyType);

    protected override void ValidateTypeInternal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        // This interceptor is created on per-service basis, so to reuse the validation cache,
        // we redirect this call to Hub.ComputeServiceInterceptor, which is a singleton.
        => Hub.Interceptor.ValidateType(type);
}
