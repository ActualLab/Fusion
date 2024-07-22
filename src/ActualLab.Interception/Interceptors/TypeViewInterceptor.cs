using System.Diagnostics.CodeAnalysis;
using ActualLab.Conversion;
using ActualLab.Interception.Internal;

namespace ActualLab.Interception.Interceptors;

public class TypeViewInterceptor : Interceptor
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    private readonly MethodInfo _createConvertingHandlerMethod;
    private readonly MethodInfo _createTaskConvertingHandlerMethod;
    private readonly MethodInfo _createValueTaskConvertingHandlerMethod;

    public TypeViewInterceptor(Options settings, IServiceProvider services)
        : base(settings, services)
    {
        MustInterceptSyncCalls = true;
        MustValidateProxyType = false;

        _createConvertingHandlerMethod = GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => string.Equals(m.Name, nameof(CreateConvertingHandler), StringComparison.Ordinal));
        _createTaskConvertingHandlerMethod = GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => string.Equals(m.Name, nameof(CreateTaskConvertingHandler), StringComparison.Ordinal));
        _createValueTaskConvertingHandlerMethod = GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(m => string.Equals(m.Name, nameof(CreateValueTaskConvertingHandler), StringComparison.Ordinal));
    }

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        Invocation initialInvocation, MethodDef methodDef)
    {
        var tTarget = initialInvocation.InterfaceProxyTarget?.GetType() ?? throw Errors.NoInterfaceProxyTarget();
        var mSource = initialInvocation.Method;
        var mArgTypes = mSource.GetParameters().Select(p => p.ParameterType).ToArray();
        var mTarget = tTarget.GetMethod(mSource.Name, mArgTypes);

        Type? GetTaskOfTArgument(Type t) {
            if (!t.IsGenericType)
                return null;
            var tg = t.GetGenericTypeDefinition();
            if (tg != typeof(Task<>))
                return null;
            return t.GetGenericArguments()[0];
        }

        Type? GetValueTaskOfTArgument(Type t) {
            if (!t.IsGenericType)
                return null;
            var tg = t.GetGenericTypeDefinition();
            if (tg != typeof(ValueTask<>))
                return null;
            return t.GetGenericArguments()[0];
        }

        if (mTarget!.ReturnType != mSource.ReturnType) {
            Func<Invocation, object?>? result;

            // Trying Task<T>
            var rtSource = GetTaskOfTArgument(mSource.ReturnType);
            var rtTarget = GetTaskOfTArgument(mTarget.ReturnType);
            if (rtSource != null && rtTarget != null) {
                result = (Func<Invocation, object?>?) _createTaskConvertingHandlerMethod
                    .MakeGenericMethod(rtSource, rtTarget)
                    .Invoke(this, [initialInvocation, mTarget]);
                if (result != null)
                    return result;
            }

            // Trying ValueTask<T>
            rtSource = GetValueTaskOfTArgument(mSource.ReturnType);
            rtTarget = GetValueTaskOfTArgument(mTarget.ReturnType);
            if (rtSource != null && rtTarget != null) {
                result = (Func<Invocation, object?>?) _createValueTaskConvertingHandlerMethod
                    .MakeGenericMethod(rtSource, rtTarget)
                    .Invoke(this, [initialInvocation, mTarget]);
                if (result != null)
                    return result;
            }

            // The only option is to convert types directly
            rtSource = mSource.ReturnType;
            rtTarget = mTarget.ReturnType;
            result = (Func<Invocation, object?>?) _createConvertingHandlerMethod
                .MakeGenericMethod(rtSource, rtTarget)
                .Invoke(this, [initialInvocation, mTarget]);
            if (result != null)
                return result;
        }

        return invocation => {
            // TODO: Get rid of reflection here (not critical)
            var target = invocation.InterfaceProxyTarget;
            return mTarget.Invoke(target, invocation.Arguments.ToArray());
        };
    }

    protected virtual Func<Invocation, object?>? CreateConvertingHandler<TSource, TTarget>(
        Invocation initialInvocation, MethodInfo mTarget)
    {
        // !!! Note that TSource is type to convert to here, and TTarget is type to convert from
        var converter = Services.Converters().From<TTarget>().To<TSource>();
        if (!converter.IsAvailable)
            return null;

        return invocation => {
            var target = invocation.InterfaceProxyTarget;
            var result = (TTarget) mTarget.Invoke(target, invocation.Arguments.ToArray())!;
            // ReSharper disable once HeapView.PossibleBoxingAllocation
            return converter.Convert(result);
        };
    }

    protected virtual Func<Invocation, object?>? CreateTaskConvertingHandler<TSource, TTarget>(
        Invocation initialInvocation, MethodInfo mTarget)
    {
        // !!! Note that TSource is type to convert to here, and TTarget is type to convert from
        var converter = Services.Converters().From<TTarget>().To<TSource>();
        if (!converter.IsAvailable)
            return null;

        return invocation => {
            var target = invocation.InterfaceProxyTarget;
            var untypedResult = mTarget.Invoke(target, invocation.Arguments.ToArray());
            var result = (Task<TTarget>) untypedResult!;
            return result.ContinueWith(
                t => converter.Convert(t.Result),
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        };
    }

    protected virtual Func<Invocation, object?>? CreateValueTaskConvertingHandler<TSource, TTarget>(
        Invocation initialInvocation, MethodInfo mTarget)
    {
        // !!! Note that TSource is type to convert to here, and TTarget is type to convert from
        var converter = Services.Converters().From<TTarget>().To<TSource>();
        if (!converter.IsAvailable)
            return null;

        return invocation => {
            var target = invocation.InterfaceProxyTarget;
            var untypedResult = mTarget.Invoke(target, invocation.Arguments.ToArray());
            var result = (ValueTask<TTarget>) untypedResult!;
            // ReSharper disable once HeapView.BoxingAllocation
            return result
                .AsTask()
                .ContinueWith(
                    t => converter.Convert(t.Result),
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
                .ToValueTask();
        };
    }
}
