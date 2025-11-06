using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;
using ActualLab.Interception.Internal;
using ActualLab.Trimming;

namespace ActualLab.Interception;

[UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "We assume proxy-related code is preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume proxy-related code is preserved")]
public partial class MethodDef
{
    private static int _lastId = 1;

    private readonly LazySlim<MethodDef, object?> _defaultResultLazy;
    private readonly LazySlim<MethodDef, object?> _defaultUnwrappedResultLazy;
    // ReSharper disable once InconsistentNaming
    protected string? _toStringCached;

    public readonly Type Type;
    public readonly MethodInfo MethodInfo;
    public readonly ParameterInfo[] Parameters;
    public readonly Type[] ParameterTypes;
    public readonly Type ReturnType;
    public readonly int CancellationTokenIndex;
    public readonly int Id;

    [field: AllowNull, MaybeNull]
    public string FullName => field ??= $"{Type.GetName()}.{MethodInfo.Name}";
    public readonly bool IsAsyncMethod;
    public readonly bool IsAsyncVoidMethod;
    public readonly bool ReturnsTask;
    public readonly bool ReturnsValueTask;
    public readonly Type? AsyncReturnTypeArgument;
    public readonly Type UnwrappedReturnType;
    public readonly bool IsUnwrappedReturnTypeClassOrNullable;
    public bool IsValid { get; init; } = true;

    public object? DefaultResult => _defaultResultLazy.Value;
    public object? DefaultUnwrappedResult => _defaultUnwrappedResultLazy.Value;
    [field: AllowNull, MaybeNull]
    public Func<object, ArgumentList, Task> TargetAsyncInvoker
        => field ??= GetCachedFunc<Func<object, ArgumentList, Task>>(typeof(TargetAsyncInvokerFactory<>));
    [field: AllowNull, MaybeNull]
    public Func<Interceptor, Invocation, Task> InterceptorAsyncInvoker
        => field ??= GetCachedFunc<Func<Interceptor, Invocation, Task>>(typeof(InterceptorAsyncInvokerFactory<>));
    [field: AllowNull, MaybeNull]
    public Func<Invocation, Task> InterceptedAsyncInvoker
        => field ??= GetCachedFunc<Func<Invocation, Task>>(typeof(InterceptedAsyncInvokerFactory<>));
    [field: AllowNull, MaybeNull]
    public Func<object, ArgumentList, ValueTask<object?>> TargetObjectAsyncInvoker
        => field ??= GetCachedFunc<Func<object, ArgumentList, ValueTask<object?>>>(typeof(TargetObjectAsyncInvokerFactory<>));
    [field: AllowNull, MaybeNull]
    public Func<Interceptor, Invocation, ValueTask<object?>> InterceptorObjectAsyncInvoker
        => field ??= GetCachedFunc<Func<Interceptor, Invocation, ValueTask<object?>>>(typeof(InterceptorObjectAsyncInvokerFactory<>));
    [field: AllowNull, MaybeNull]
    public Func<Invocation, ValueTask<object?>> InterceptedObjectAsyncInvoker
        => field ??= GetCachedFunc<Func<Invocation, ValueTask<object?>>>(typeof(InterceptedObjectAsyncInvokerFactory<>));
    [field: AllowNull, MaybeNull]
    public Func<Task, object?> UniversalAsyncResultWrapper
        => field ??= GetCachedFunc<Func<Task, object?>>(typeof(UniversalAsyncResultWrapperFactory<>));
    [field: AllowNull, MaybeNull]
    public Func<Task, ValueTask<object?>> TaskToUntypedValueTaskConverter
        => field ??= GenericInstanceCache
            .Get<Func<Task, ValueTask<object?>>>(typeof(TaskExt.ToUntypedValueTaskFactory<>), UnwrappedReturnType);

    // Must be on KeepCodeForResult<,>, but since we can't use any params there...
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Result))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Result<>))]
    public MethodDef(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo methodInfo)
    {
        Id = Interlocked.Increment(ref _lastId);

        var parameters = methodInfo.GetParameters();
        var ctIndex = -1;
        for (var i = parameters.Length - 1; i >= 0; i--) {
            var p = parameters[i];
            if (typeof(CancellationToken).IsAssignableFrom(p.ParameterType)) {
                ctIndex = i;
                break;
            }
        }
        CancellationTokenIndex = ctIndex;

        var parameterTypes = new Type[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            parameterTypes[i] = parameters[i].ParameterType;

        Type = type;
        MethodInfo = methodInfo;
        Parameters = parameters;
        ParameterTypes = parameterTypes;

        ReturnType = methodInfo.ReturnType;
        if (!ReturnType.IsGenericType) {
            ReturnsTask = ReturnType == typeof(Task);
            ReturnsValueTask = ReturnType == typeof(ValueTask);
            IsAsyncMethod = IsAsyncVoidMethod = ReturnsTask || ReturnsValueTask;
        }
        else {
            var returnTypeGtd = ReturnType.GetGenericTypeDefinition();
            ReturnsTask = returnTypeGtd == typeof(Task<>);
            ReturnsValueTask = returnTypeGtd == typeof(ValueTask<>);
            IsAsyncMethod = ReturnsTask || ReturnsValueTask;
            IsAsyncVoidMethod = false;
        }
        AsyncReturnTypeArgument = IsAsyncMethod
            ? IsAsyncVoidMethod ? typeof(void) : ReturnType.GetGenericArguments()[0]
            : null;
        UnwrappedReturnType = AsyncReturnTypeArgument ?? ReturnType;
        if (UnwrappedReturnType == typeof(void))
            UnwrappedReturnType = typeof(Unit);
        IsUnwrappedReturnTypeClassOrNullable = UnwrappedReturnType.IsClass
            || (UnwrappedReturnType.IsGenericType && UnwrappedReturnType.GetGenericTypeDefinition() == typeof(Nullable<>));

        _defaultResultLazy = new LazySlim<MethodDef, object?>(this, static self => self.GetDefaultResult());
        _defaultUnwrappedResultLazy = new LazySlim<MethodDef, object?>(this, static self => self.GetDefaultUnwrappedResult());
    }

    public override string ToString()
        => _toStringCached ??= string.Concat(
            GetType().Name,
            "(",
            FullName,
            ")",
            IsValid ? "" : "-invalid");

    public sealed override int GetHashCode()
        => Id;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsInstanceOfUnwrappedReturnType(object? candidate)
        => candidate is null
            ? IsUnwrappedReturnTypeClassOrNullable
            : UnwrappedReturnType.IsInstanceOfType(candidate);

    public object? WrapResult<TUnwrapped>(TUnwrapped result)
    {
        if (!IsAsyncMethod)
            return result;

        return ReturnsTask
            ? Task.FromResult(result)
            : IsAsyncVoidMethod
                ? ValueTaskExt.CompletedTask
                : ValueTaskExt.FromResult(result);
    }

    public object? WrapAsyncInvokerResult<TUnwrapped>(Task<TUnwrapped> resultTask)
    {
        if (!IsAsyncMethod) {
            var taskAwaiter = resultTask.GetAwaiter();
            return taskAwaiter.IsCompleted
                ? taskAwaiter.GetResult()
                : throw Errors.SyncMethodResultTaskMustBeCompleted();
        }

        return ReturnsTask
            ? resultTask
            : IsAsyncVoidMethod
                ? ((Task)resultTask).ToValueTask()
                : resultTask.ToValueTask();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? WrapResultOfAsyncMethod<TUnwrapped>(TUnwrapped result)
        => ReturnsTask
            ? Task.FromResult(result)
            : IsAsyncVoidMethod
                ? ValueTaskExt.CompletedTask
                : ValueTaskExt.FromResult(result);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? WrapAsyncInvokerResultOfAsyncMethod<TUnwrapped>(Task<TUnwrapped> resultTask)
        => ReturnsTask
            ? resultTask
            : IsAsyncVoidMethod
                ? ((Task)resultTask).ToValueTask()
                : resultTask.ToValueTask();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? WrapAsyncInvokerResultOfAsyncMethodUntyped(Task untypedResultTask)
        => ReturnsTask
            ? untypedResultTask
            : untypedResultTask.ToTypedValueTask(AsyncReturnTypeArgument!);

    public Func<Invocation, Task<TUnwrapped>>? SelectAsyncInvoker<TUnwrapped>(
        object proxy,
        object? target = null)
    {
        if (target is Interceptor interceptor) {
            // Interceptor is available -> invoke it
            var invoker = (Func<Interceptor, Invocation, Task<TUnwrapped>>)InterceptorAsyncInvoker;
            return invocation => invoker.Invoke(interceptor, invocation);
        }

        if (!ReferenceEquals(target, null) && !ReferenceEquals(target, proxy)) {
            // There is a target, and the target is not proxy -> invoke its method
            var invoker = (Func<object, ArgumentList, Task<TUnwrapped>>)TargetAsyncInvoker;
            return invocation => invoker.Invoke(target, invocation.Arguments);
        }

        // No target -> invoke intercepted method
        if (proxy is not InterfaceProxy)
            return (Func<Invocation, Task<TUnwrapped>>)InterceptedAsyncInvoker;

        // Nothing to invoke
        return null;
    }

    public Func<Invocation, Task>? SelectAsyncInvokerUntyped(
        object proxy,
        object? target = null)
    {
        if (target is Interceptor interceptor) {
            // Interceptor is available -> invoke it
            var invoker = InterceptorAsyncInvoker;
            return invocation => invoker.Invoke(interceptor, invocation);
        }

        if (!ReferenceEquals(target, null) && !ReferenceEquals(target, proxy)) {
            // There is a target, and the target is not proxy -> invoke its method
            var invoker = TargetAsyncInvoker;
            return invocation => invoker.Invoke(target, invocation.Arguments);
        }

        // No target -> invoke intercepted method
        if (proxy is not InterfaceProxy)
            return InterceptedAsyncInvoker;

        // Nothing to invoke
        return null;
    }

    // Protected methods

    protected internal virtual void KeepCodeForResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep<TResult>();
        CodeKeeper.Keep<TUnwrapped>();
        CodeKeeper.Keep<Result>();
        CodeKeeper.Keep<Result<TUnwrapped>>();
        CodeKeeper.Keep<Result<Task<TUnwrapped>>>();
        CodeKeeper.Keep<Result<ValueTask<TUnwrapped>>>();

        WrapResult<TUnwrapped>(default!);
        WrapAsyncInvokerResult<TUnwrapped>(default!);
        WrapResultOfAsyncMethod<TUnwrapped>(default!);
        WrapAsyncInvokerResultOfAsyncMethod<TUnwrapped>(default!);
        SelectAsyncInvoker<TUnwrapped>(null!);
        GetCachedFunc<TUnwrapped>(null!);

        CodeKeeper.Keep<TargetAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<InterceptorAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<InterceptedAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<TargetObjectAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<InterceptorObjectAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<InterceptedObjectAsyncInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<UniversalAsyncResultWrapperFactory<TUnwrapped>>();
    }

    // Private methods

    private object? GetDefaultResult()
        => !IsAsyncMethod
            ? DefaultUnwrappedResult
            : ReturnsValueTask
                ? IsAsyncVoidMethod ? default(ValueTask) : ValueTaskExt.FromDefaultResult(UnwrappedReturnType)
                : TaskExt.FromDefaultResult(UnwrappedReturnType);

    private object? GetDefaultUnwrappedResult()
        => UnwrappedReturnType.GetDefaultValue();

    private TResult GetCachedFunc<TResult>(Type factoryType)
        => GenericInstanceCache.Get<Func<MethodDef, TResult>>(factoryType, UnwrappedReturnType).Invoke(this);
}
