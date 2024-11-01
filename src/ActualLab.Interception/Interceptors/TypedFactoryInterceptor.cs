using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Interception.Interceptors;

public class TypedFactoryInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    public TypedFactoryInterceptor(Options settings, IServiceProvider services)
        : base(settings, services)
    {
        MustInterceptAsyncCalls = false;
        MustInterceptSyncCalls = true;
    }

    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
    {
        var methodDef = base.CreateMethodDef(method, proxyType);
        if (methodDef?.ReturnType == typeof(void))
            methodDef = null;
        return methodDef;
    }

    protected internal override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
    {
        var parameters = methodDef.Parameters;
        var parameterTypes = new Type[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
            parameterTypes[i] = parameters[i].ParameterType;
        var factory = ActivatorUtilities.CreateFactory(methodDef.UnwrappedReturnType, parameterTypes);
        return invocation => factory.Invoke(Services, invocation.Arguments.ToArray());
    }
}
