using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Interception;

/// <summary>
/// An interceptor for remote compute services that delegates calls to either
/// the local compute method handler or the RPC interceptor.
/// </summary>
public class RemoteComputeServiceInterceptor : ComputeServiceInterceptor
{
    /// <summary>
    /// Configuration options for <see cref="RemoteComputeServiceInterceptor"/>.
    /// </summary>
    public new record Options : ComputeServiceInterceptor.Options
    {
        public static new Options Default { get; set; } = new();

        public (LogLevel LogLevel, int MaxDataLength) LogCacheEntryUpdateSettings { get; init; } = (LogLevel.None, 0);
    }

    public readonly RpcServiceDef RpcServiceDef;
    public readonly RpcInterceptor RpcInterceptor;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RemoteComputeServiceInterceptor(Options settings, FusionHub hub, RpcInterceptor rpcInterceptor)
        : base(settings, hub)
    {
        RpcServiceDef = rpcInterceptor.ServiceDef;
        RpcInterceptor = rpcInterceptor;
    }

    public override Func<Invocation, object?>? SelectHandler(in Invocation invocation)
        => GetHandler(invocation) // Compute service method
            ?? RpcInterceptor.SelectHandler(invocation); // Regular or command service method

    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume proxy-related code is preserved")]
    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var computeMethodDef = (ComputeMethodDef)methodDef;
        var rpcMethodDef = RpcServiceDef.FindMethod(initialInvocation.Method);
        if (rpcMethodDef is null) {
            // Proxy is a Distributed service, and a non-RPC method is called (i.e., the local compute method)
            var function = computeMethodDef.CreateComputeMethodFunction(Hub);
            return function.ComputeServiceInterceptorHandler;
        }
        else {
            var function = computeMethodDef.CreateRemoteComputeMethodFunction(Hub, rpcMethodDef);
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
