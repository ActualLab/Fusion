using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Interception.Internal;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#endif
public abstract class RpcInterceptorBase : Interceptor
{
    public new record Options : Interceptor.Options;

    public readonly RpcHub Hub;
    public readonly RpcServiceDef ServiceDef;

    // ReSharper disable once ConvertToPrimaryConstructor
    protected RpcInterceptorBase(Options settings, IServiceProvider services, RpcServiceDef serviceDef)
        : base(settings, services)
    {
        Hub = services.RpcHub();
        ServiceDef = serviceDef;
    }

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
    {
        var methodDef = ServiceDef.GetMethod(method);
        if (methodDef != null)
            return methodDef;
        if (!method.IsPublic || typeof(InterfaceProxy).IsAssignableFrom(proxyType))
            return null;

        // It's a class proxy, let's try to map the method to interface
        var parameters = method.GetParameters();
        foreach (var m in ServiceDef.Methods) {
            if (!m.Method.Name.Equals(method.Name, StringComparison.Ordinal))
                continue;

            if (m.Parameters.Length != parameters.Length)
                continue;

            var isMatch = true;
            for (var i = 0; i < parameters.Length; i++) {
                isMatch &= m.Parameters[i].ParameterType == parameters[i].ParameterType;
                if (!isMatch)
                    break;
            }
            if (isMatch)
                return m;
        }

        return null;
    }
}
