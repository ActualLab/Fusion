using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Interception;
using ActualLab.Fusion.Interception;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Interception;

public class HybridComputeServiceInterceptor : ComputeServiceInterceptor
{
    public new record Options : ComputeServiceInterceptor.Options
    {
        public static new Options Default { get; set; } = new();
    }

    public readonly RpcInterceptor RpcInterceptor;
    public readonly RpcHub RpcHub;

    // ReSharper disable once ConvertToPrimaryConstructor
    public HybridComputeServiceInterceptor(
        Options settings,
        IServiceProvider services,
        RpcInterceptor rpcInterceptor
        ) : base(settings, services)
    {
        RpcInterceptor = rpcInterceptor;
        RpcHub = rpcInterceptor.Hub;
        CommandServiceInterceptor = new CommandServiceInterceptor(Hub.CommandServiceInterceptorOptions, services) {
            Next = RpcInterceptor,
        };
    }

    public override Func<Invocation, object?>? SelectHandler(Invocation invocation)
        => GetHandler(invocation) // Compute method
            ?? CommandServiceInterceptor.GetHandler(invocation) // Command method
            ?? RpcInterceptor.GetHandler(invocation); // Regular method

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var computeMethodDef = (ComputeMethodDef)methodDef;
        var rpcMethodDef = (RpcMethodDef?)RpcInterceptor.GetMethodDef(initialInvocation.Method, initialInvocation.Proxy.GetType());
        var function = rpcMethodDef == null
            ? new ComputeMethodFunction<TUnwrapped>(computeMethodDef, Services) // It's always a local call
            : new HybridComputeMethodFunction<TUnwrapped>(computeMethodDef, rpcMethodDef, Hub.ClientComputedCache, Services);
        return CreateHandler(function);
    }

    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
        // This interceptor is created on per-service basis, so to reuse the validation cache,
        // we redirect this call to Hub.ComputeServiceInterceptor, which is a singleton.
        => Hub.ComputeServiceInterceptor.GetMethodDef(method, proxyType);

    protected override void ValidateTypeInternal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        // This interceptor is created on per-service basis, so to reuse the validation cache,
        // we redirect this call to Hub.ComputeServiceInterceptor, which is a singleton.
        => Hub.ComputeServiceInterceptor.ValidateType(type);
}
