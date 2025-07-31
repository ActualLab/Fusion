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
    public readonly RpcInterceptor NonComputeCallInterceptor;
    public readonly RpcInterceptor ComputeCallInterceptor;
    public readonly object? LocalTarget;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RemoteComputeServiceInterceptor(Options settings,
        FusionHub hub,
        RpcInterceptor nonComputeCallInterceptor,
        RpcInterceptor computeCallInterceptor,
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

    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume proxy-related code is preserved")]
    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var computeMethodDef = (ComputeMethodDef)methodDef;
        var rpcMethodDef = RpcServiceDef.GetOrFindMethod(initialInvocation.Method);
        if (rpcMethodDef is null) {
            // Proxy is a Distributed service, and a non-RPC method is called (i.e., the local compute method)
            var function = (ComputeMethodFunction)typeof(ComputeMethodFunction<>)
                .MakeGenericType(computeMethodDef.UnwrappedReturnType)
                .CreateInstance(Hub, computeMethodDef);
            return function.ComputeServiceInterceptorHandler;
        }
        else {
            var function = (RemoteComputeMethodFunction)typeof(RemoteComputeMethodFunction<>)
                .MakeGenericType(computeMethodDef.UnwrappedReturnType)
                .CreateInstance(Hub, computeMethodDef, rpcMethodDef, LocalTarget);
            return function.RemoteComputeServiceInterceptorHandler;
        }
    }

    protected override MethodDef? CreateMethodDef(MethodInfo method,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyType)
        // This interceptor is created on a per-service basis, so to reuse the validation cache,
        // we redirect this call to Hub.ComputeServiceInterceptor, which is a singleton.
        => Hub.ComputeServiceInterceptor.GetMethodDef(method, proxyType);

    protected override void ValidateTypeInternal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        // This interceptor is created on a per-service basis, so to reuse the validation cache,
        // we redirect this call to Hub.ComputeServiceInterceptor, which is a singleton.
        => Hub.ComputeServiceInterceptor.ValidateType(type);
}
