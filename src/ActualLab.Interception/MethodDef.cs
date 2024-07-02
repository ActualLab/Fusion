using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Internal;
using InvalidCastException = System.InvalidCastException;

namespace ActualLab.Interception;

public class MethodDef
{
    private static readonly ConcurrentDictionary<(MethodInfo, Type), Func<MethodDef, object>> AsyncInvokerCache = new();
    private static readonly MethodInfo CreateTargetAsyncInvokerMethod =
        typeof(MethodDef).GetMethod(nameof(CreateTargetAsyncInvoker), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo CreateInterceptorAsyncInvokerMethod =
        typeof(MethodDef).GetMethod(nameof(CreateInterceptorAsyncInvoker), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo CreateInterceptedAsyncInvokerMethod =
        typeof(MethodDef).GetMethod(nameof(CreateInterceptedAsyncInvoker), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static int _lastId = 1;

    private string? _fullName;
    private Func<object, ArgumentList, Task>? _targetAsyncInvoker;
    private Func<Interceptor, Invocation, Task>? _interceptorAsyncInvoker;
    private Func<Invocation, Task>? _interceptedAsyncInvoker;
    private readonly LazySlim<MethodDef, object?> _defaultResultLazy;
    private readonly LazySlim<MethodDef, object?> _defaultUnwrappedResultLazy;

    public readonly Type Type;
    public readonly MethodInfo Method;
    public readonly ParameterInfo[] Parameters;
    public readonly Type[] ParameterTypes;
    public readonly Type ReturnType;
    public readonly int CancellationTokenIndex;
    public readonly int Id;

    public string FullName => _fullName ??= $"{Type.GetName()}.{Method.Name}";
    public readonly bool IsAsyncMethod;
    public readonly bool IsAsyncVoidMethod;
    public readonly bool ReturnsTask;
    public readonly bool ReturnsValueTask;
    public readonly Type UnwrappedReturnType;
    public bool IsValid { get; init; } = true;

    public object? DefaultResult => _defaultResultLazy.Value;
    public object? DefaultUnwrappedResult => _defaultUnwrappedResultLazy.Value;
    public Func<object, ArgumentList, Task> TargetAsyncInvoker
        => _targetAsyncInvoker ??= GetAsyncInvoker<Func<object, ArgumentList, Task>>(CreateTargetAsyncInvokerMethod);
    public Func<Interceptor, Invocation, Task> InterceptorAsyncInvoker
        => _interceptorAsyncInvoker ??= GetAsyncInvoker<Func<Interceptor, Invocation, Task>>(CreateInterceptorAsyncInvokerMethod);
    public Func<Invocation, Task> InterceptedAsyncInvoker
        => _interceptedAsyncInvoker ??= GetAsyncInvoker<Func<Invocation, Task>>(CreateInterceptedAsyncInvokerMethod);

    public MethodDef(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo method)
    {
        Id = Interlocked.Increment(ref _lastId);

        var parameters = method.GetParameters();
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
        Method = method;
        Parameters = parameters;
        ParameterTypes = parameterTypes;

        ReturnType = method.ReturnType;
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
        UnwrappedReturnType = IsAsyncMethod
            ? IsAsyncVoidMethod ? typeof(Unit) : ReturnType.GetGenericArguments()[0]
            : ReturnType;
        _defaultResultLazy = new LazySlim<MethodDef, object?>(this, static self => self.GetDefaultResult());
        _defaultUnwrappedResultLazy = new LazySlim<MethodDef, object?>(this, static self => self.GetDefaultUnwrappedResult());
    }

    public override string ToString()
        => $"{GetType().Name}({FullName}){(IsValid ? "" : " - invalid")}";

    public sealed override int GetHashCode()
        => Id;

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
            // There is target & target is not proxy -> invoke its method
            var invoker = (Func<object, ArgumentList, Task<TUnwrapped>>)TargetAsyncInvoker;
            return invocation => invoker.Invoke(target, invocation.Arguments);
        }

        // No target -> invoke intercepted method
        if (proxy is not InterfaceProxy)
            return (Func<Invocation, Task<TUnwrapped>>)InterceptedAsyncInvoker;

        // Nothing to invoke
        return null;
    }

    // Private methods

    private object? GetDefaultResult()
        => !IsAsyncMethod
            ? DefaultUnwrappedResult
            : ReturnsValueTask
                ? ValueTaskExt.FromDefaultResult(UnwrappedReturnType)
                : TaskExt.FromDefaultResult(UnwrappedReturnType);

    private object? GetDefaultUnwrappedResult()
        => UnwrappedReturnType.IsClass
            ? null!
            : Activator.CreateInstance(UnwrappedReturnType);

    private TResult GetAsyncInvoker<TResult>(MethodInfo methodInfo)
        => (TResult)AsyncInvokerCache.GetOrAdd((methodInfo, UnwrappedReturnType),
            static key => {
                var (methodInfo1, returnType) = key;
                return (Func<MethodDef, object>)methodInfo1
                    .MakeGenericMethod(returnType)
                    .CreateDelegate(typeof(Func<MethodDef, object>), null);
            }).Invoke(this);

    private static Func<object, ArgumentList, Task<TUnwrapped>> CreateTargetAsyncInvoker<TUnwrapped>(MethodDef methodDef)
    {
        if (methodDef.ReturnsTask) {
            if (methodDef.IsAsyncVoidMethod)
                return (service, args) => {
                    var result = ((Task)args.GetInvoker(methodDef.Method).Invoke(service, args)!).ToUnitTask();
                    return result as Task<TUnwrapped> ?? throw new InvalidCastException();
                };
            return (service, args) => (Task<TUnwrapped>)args.GetInvoker(methodDef.Method).Invoke(service, args)!;
        }

        if (methodDef.ReturnsValueTask) {
            if (methodDef.IsAsyncVoidMethod)
                return (service, args) => {
                    var result = ((ValueTask)args.GetInvoker(methodDef.Method).Invoke(service, args)!).ToUnitTask();
                    return result as Task<TUnwrapped> ?? throw new InvalidCastException();
                };
            return (service, args) => ((ValueTask<TUnwrapped>)args.GetInvoker(methodDef.Method).Invoke(service, args)!).AsTask();
        }

        // Non-async method
        return (service, args) => {
            var result = Task.FromResult(args.GetInvoker(methodDef.Method).Invoke(service, args));
            return result as Task<TUnwrapped> ?? throw new InvalidCastException();
        };
    }

    private static Func<Interceptor, Invocation, Task<TUnwrapped>> CreateInterceptorAsyncInvoker<TUnwrapped>(MethodDef methodDef)
    {
        if (methodDef.ReturnsTask)
            return methodDef.IsAsyncVoidMethod
                ? (interceptor, invocation) => {
                    var result = interceptor.Intercept<Task>(invocation).ToUnitTask();
                    return result as Task<TUnwrapped> ?? throw new InvalidCastException();
                }
                : (interceptor, invocation) => interceptor.Intercept<Task<TUnwrapped>>(invocation);

        if (methodDef.ReturnsValueTask)
            return methodDef.IsAsyncVoidMethod
                ? (interceptor, invocation) => {
                    var result = interceptor.Intercept<ValueTask>(invocation).ToUnitTask();
                    return result as Task<TUnwrapped> ?? throw new InvalidCastException();
                }
                : (interceptor, invocation) => interceptor.Intercept<ValueTask<TUnwrapped>>(invocation).AsTask();

        if (methodDef.ReturnType == typeof(void))
            return (interceptor, invocation) => {
                interceptor.Intercept(invocation);
                return TaskExt.UnitTask as Task<TUnwrapped> ?? throw new InvalidCastException();
            };

        return (interceptor, invocation) => Task.FromResult(interceptor.Intercept<TUnwrapped>(invocation));
    }

    private static Func<Invocation, Task<TUnwrapped>> CreateInterceptedAsyncInvoker<TUnwrapped>(MethodDef methodDef)
    {
        if (methodDef.ReturnsTask)
            return methodDef.IsAsyncVoidMethod
                ? invocation => {
                    var result = invocation.Intercepted<Task>().ToUnitTask();
                    return result as Task<TUnwrapped> ?? throw new InvalidCastException();
                }
                : invocation => invocation.Intercepted<Task<TUnwrapped>>();

        if (methodDef.ReturnsValueTask)
            return methodDef.IsAsyncVoidMethod
                ? invocation => {
                    var result = invocation.Intercepted<ValueTask>().ToUnitTask();
                    return result as Task<TUnwrapped> ?? throw new InvalidCastException();
                }
                : invocation => invocation.Intercepted<ValueTask<TUnwrapped>>().AsTask();

        if (methodDef.ReturnType == typeof(void))
            return invocation => {
                invocation.Intercepted();
                return TaskExt.UnitTask as Task<TUnwrapped> ?? throw new InvalidCastException();
            };

        return invocation => Task.FromResult(invocation.Intercepted<TUnwrapped>());
    }
}
