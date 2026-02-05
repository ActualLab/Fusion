using ActualLab.CommandR.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.OS;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Interception;

/// <summary>
/// An interceptor that routes compute method calls through <see cref="ComputeMethodFunction"/>
/// to produce cached <see cref="Computed"/> values.
/// </summary>
public class ComputeServiceInterceptor : Interceptor
{
    /// <summary>
    /// Configuration options for <see cref="ComputeServiceInterceptor"/>.
    /// </summary>
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new() {
            // This interceptor is shared, so we adjust its cache concurrency settings
            HandlerCacheConcurrencyLevel = HardwareInfo.GetProcessorCountPo2Factor(2),
            HandlerCacheCapacity = 131,
        };
    }

    public readonly FusionHub Hub;
    public readonly CommandServiceInterceptor CommandServiceInterceptor;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ComputeServiceInterceptor(Options settings, FusionHub hub)
        : base(settings, hub.Services)
    {
        Hub = hub;
        CommandServiceInterceptor = Hub.CommanderHub.Interceptor;
        UsesUntypedHandlers = true;
    }

    public override Func<Invocation, object?>? SelectHandler(in Invocation invocation)
        => GetHandler(invocation) ?? CommandServiceInterceptor.SelectHandler(invocation);

    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume proxy-related code is preserved")]
    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var computeMethodDef = (ComputeMethodDef)methodDef;
        var function = computeMethodDef.CreateComputeMethodFunction(Hub);
        return function.ComputeServiceInterceptorHandler;
    }

    // We don't need to decorate this method with any dynamic access attributes
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume proxy-related code is preserved")]
    protected override MethodDef? CreateMethodDef(MethodInfo method,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyType)
    {
        var type = proxyType.NonProxyType();
        var options = Hub.ComputedOptionsProvider.GetComputedOptions(type, method);
        if (options is null)
            return null;

        var methodDef = new ComputeMethodDef(type, method, this);
        return methodDef.IsValid ? methodDef : null;
    }

    protected override void ValidateTypeInternal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        CommandServiceInterceptor.ValidateType(type);
        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var methods = (type.IsInterface
                ? type.GetAllInterfaceMethods(bindingFlags)
                : type.GetMethods(bindingFlags)
            ).ToList();
        foreach (var method in methods) {
            if (method.DeclaringType == typeof(object))
                continue;
            var options = Hub.ComputedOptionsProvider.GetComputedOptions(type, method);
            if (options is null)
                continue;

            if (method.IsStatic)
                throw Errors.ComputeMethodAttributeOnStaticMethod(method);
            if (!method.IsVirtual)
                throw Errors.ComputeMethodAttributeOnNonVirtualMethod(method);
            if (method.IsFinal)
                // All implemented interface members are marked as "virtual final"
                // unless they are truly virtual
                throw Errors.ComputeMethodAttributeOnNonVirtualMethod(method);

            var returnType = method.ReturnType;
            if (!returnType.IsTaskOrValueTask())
                throw Errors.ComputeMethodAttributeOnNonAsyncMethod(method);

            var unwrappedReturnType = returnType.GetTaskOrValueTaskArgument();
            if (unwrappedReturnType is null)
                throw Errors.ComputeMethodAttributeOnAsyncMethodReturningNonGenericTask(method);
            if (unwrappedReturnType == typeof(RpcNoWait))
                throw Errors.ComputeMethodAttributeOnAsyncMethodReturningRpcNoWait(method);

            Log.IfEnabled(ValidationLogLevel)?.Log(ValidationLogLevel,
                "+ {Method}: {Options}", method.ToShortString(), options);
        }
    }
}
