using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

public class ComputeServiceInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public readonly FusionInternalHub Hub;
    public readonly CommandServiceInterceptor CommandServiceInterceptor;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ComputeServiceInterceptor(Options settings, IServiceProvider services)
        : base(settings, services)
    {
        Hub = services.GetRequiredService<FusionInternalHub>();
        CommandServiceInterceptor = Hub.CommandServiceInterceptor;
    }

    public override Func<Invocation, object?>? SelectHandler(Invocation invocation)
        => GetHandler(invocation) ?? CommandServiceInterceptor.SelectHandler(invocation);

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var function = new ComputeMethodFunction<TUnwrapped>((ComputeMethodDef)methodDef, Services);
        return CreateHandler(function);
    }

    protected static Func<Invocation, object?> CreateHandler<TUnwrapped>(ComputeMethodFunction<TUnwrapped> function)
        => invocation => {
            var methodDef = function.MethodDef;
            var input = new ComputeMethodInput(function, methodDef, invocation);
            var arguments = input.Arguments;
            var ctIndex = methodDef.CancellationTokenIndex;
            var cancellationToken = ctIndex >= 0
                ? arguments.GetCancellationToken(ctIndex)
                : default;

            try {
                // InvokeAndStrip allows to get rid of one extra allocation
                // of a task stripping the result of regular Invoke.
                var task = function.InvokeAndStrip(input, ComputeContext.Current, cancellationToken);
                // ReSharper disable once HeapView.BoxingAllocation
                return methodDef.ReturnsValueTask ? new ValueTask<TUnwrapped>(task) : task;
            }
            finally {
                if (cancellationToken != default)
                    // We don't want memory leaks + unexpected cancellation later
                    arguments.SetCancellationToken(ctIndex, default);
            }
        };

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Type proxyType)
    {
        var type = proxyType.NonProxyType();
#pragma warning disable IL2072
        var options = Hub.ComputedOptionsProvider.GetComputedOptions(type, method);
        if (options == null)
            return null;

        var methodDef = new ComputeMethodDef(type, method, this);
#pragma warning restore IL2072
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
            if (options == null)
                continue;

            if (method.IsStatic)
                throw Errors.ComputeServiceMethodAttributeOnStaticMethod(method);
            if (!method.IsVirtual)
                throw Errors.ComputeServiceMethodAttributeOnNonVirtualMethod(method);
            if (method.IsFinal)
                // All implemented interface members are marked as "virtual final"
                // unless they are truly virtual
                throw Errors.ComputeServiceMethodAttributeOnNonVirtualMethod(method);

            var returnType = method.ReturnType;
            if (!returnType.IsTaskOrValueTask())
                throw Errors.ComputeServiceMethodAttributeOnNonAsyncMethod(method);
            if (returnType.GetTaskOrValueTaskArgument() == null)
                throw Errors.ComputeServiceMethodAttributeOnAsyncMethodReturningNonGenericTask(method);

            Log.IfEnabled(ValidationLogLevel)?.Log(ValidationLogLevel,
                "+ {Method}: {Options}", method.ToString(), options);
        }
    }
}
