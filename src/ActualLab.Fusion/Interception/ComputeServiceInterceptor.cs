using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Interception;

public class ComputeServiceInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    public readonly FusionHub Hub;
    public readonly CommandServiceInterceptor CommandServiceInterceptor;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ComputeServiceInterceptor(Options settings, FusionHub hub)
        : base(settings, hub.Services)
    {
        Hub = hub;
        CommandServiceInterceptor = Hub.CommanderHub.Interceptor;
    }

    public override Func<Invocation, object?>? SelectHandler(in Invocation invocation)
        => GetHandler(invocation) ?? CommandServiceInterceptor.SelectHandler(invocation);

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var function = new ComputeMethodFunction<TUnwrapped>((ComputeMethodDef)methodDef, Hub);
        return CreateHandler(function);
    }

    protected static Func<Invocation, object?> CreateHandler<TUnwrapped>(ComputeMethodFunction<TUnwrapped> function)
    {
        var methodDef = function.MethodDef;
        var ctIndex = methodDef.CancellationTokenIndex;
        return ctIndex >= 0
            ? RegularHandler
            : NoCancellationTokenHandler;

        object? NoCancellationTokenHandler(Invocation invocation) {
            var input = new ComputeMethodInput(function, methodDef, invocation);
            // Inlined:
            // var task = function.InvokeAndStrip(input, ComputeContext.Current, default);
            var context = ComputeContext.Current;
            var computed = ComputedRegistry.Instance.Get(input) as Computed<TUnwrapped>; // = input.GetExistingComputed()
            var task = ComputedImpl.TryUseExisting(computed, context)
                ? ComputedImpl.StripToTask(computed, context)
                : function.TryRecompute(input, context, default);
            // ReSharper disable once HeapView.BoxingAllocation
            return methodDef.ReturnsValueTask ? new ValueTask<TUnwrapped>(task) : task;
        }

        object? RegularHandler(Invocation invocation) {
            var input = new ComputeMethodInput(function, methodDef, invocation);
            var arguments = invocation.Arguments;
            var cancellationToken = arguments.GetCancellationToken(ctIndex);
            try {
                // Inlined:
                // var task = function.InvokeAndStrip(input, ComputeContext.Current, cancellationToken);
                var context = ComputeContext.Current;
                var computed = ComputedRegistry.Instance.Get(input) as Computed<TUnwrapped>; // = input.GetExistingComputed()
                var task = ComputedImpl.TryUseExisting(computed, context)
                    ? ComputedImpl.StripToTask(computed, context)
                    : function.TryRecompute(input, context, cancellationToken);
                // ReSharper disable once HeapView.BoxingAllocation
                return methodDef.ReturnsValueTask ? new ValueTask<TUnwrapped>(task) : task;
            }
            finally {
                if (cancellationToken.CanBeCanceled)
                    // ComputedInput is stored in ComputeRegistry, so we remove CancellationToken there
                    // to prevent memory leaks + possible unexpected cancellations on .Update calls.
                    arguments.SetCancellationToken(ctIndex, default);
            }
        }
    }

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
            if (unwrappedReturnType == null)
                throw Errors.ComputeMethodAttributeOnAsyncMethodReturningNonGenericTask(method);
            if (unwrappedReturnType == typeof(RpcNoWait))
                throw Errors.ComputeMethodAttributeOnAsyncMethodReturningRpcNoWait(method);

            Log.IfEnabled(ValidationLogLevel)?.Log(ValidationLogLevel,
                "+ {Method}: {Options}", method.ToString(), options);
        }
    }
}
