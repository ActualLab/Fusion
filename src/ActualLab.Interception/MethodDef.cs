using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

public abstract class MethodDef
{
    private static readonly MethodInfo InvokeMethod =
        typeof(MethodDef).GetMethod(nameof(InvokeAsync), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<Type, Func<MethodDef, object, ArgumentList, Task>> InvokerCache = new();

    private string? _fullName;
    private Func<object, ArgumentList, Task>? _invoker;

    public readonly Type Type;
    public readonly MethodInfo Method;
    public readonly ParameterInfo[] Parameters;
    public readonly Type[] ParameterTypes;
    public int CancellationTokenIndex { get; init; } = -1;

    public string FullName => _fullName ??= $"{Type.GetName()}.{Method.Name}";
    public readonly bool IsAsyncMethod;
    public readonly bool IsAsyncVoidMethod;
    public readonly bool ReturnsTask;
    public readonly bool ReturnsValueTask;
    public readonly Type UnwrappedReturnType;
    public Func<object, ArgumentList, Task> AsyncInvoker => _invoker ??= CreateAsyncInvoker();

    public bool IsValid { get; init; } = true;

    protected MethodDef(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo method)
    {
        var parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++) {
            var p = parameters[i];
            if (typeof(CancellationToken).IsAssignableFrom(p.ParameterType))
                CancellationTokenIndex = i;
        }
        var parameterTypes = new Type[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            parameterTypes[i] = parameters[i].ParameterType;

        Type = type;
        Method = method;
        Parameters = parameters;
        ParameterTypes = parameterTypes;

        var returnType = method.ReturnType;
        if (!returnType.IsGenericType) {
            ReturnsTask = returnType == typeof(Task);
            ReturnsValueTask = returnType == typeof(ValueTask);
            IsAsyncMethod = IsAsyncVoidMethod = ReturnsTask || ReturnsValueTask;
        }
        else {
            var returnTypeGtd = returnType.GetGenericTypeDefinition();
            ReturnsTask = returnTypeGtd == typeof(Task<>);
            ReturnsValueTask = returnTypeGtd == typeof(ValueTask<>);
            IsAsyncMethod = ReturnsTask || ReturnsValueTask;
            IsAsyncVoidMethod = false;
        }
        UnwrappedReturnType = IsAsyncMethod
            ? IsAsyncVoidMethod ? typeof(Unit) : returnType.GetGenericArguments()[0]
            : returnType;
    }

    public override string ToString()
        => $"{GetType().Name}({FullName}){(IsValid ? "" : " - invalid")}";

    public object? UnwrapAsyncInvokerResult<TResult>(Task<TResult> asyncInvokerResult)
    {
        if (IsAsyncMethod)
            return ReturnsTask
                ? asyncInvokerResult
                : IsAsyncVoidMethod
                    ? ((Task)asyncInvokerResult).ToValueTask()
                    : asyncInvokerResult.ToValueTask();

        var taskAwaiter = asyncInvokerResult.GetAwaiter();
        return taskAwaiter.IsCompleted
            ? taskAwaiter.GetResult()
            : throw Errors.SyncMethodResultTaskMustBeCompleted();
    }

    // Private methods

    private Func<object, ArgumentList, Task> CreateAsyncInvoker()
    {
        var staticInvoker = InvokerCache.GetOrAdd(UnwrappedReturnType,
            tResult => (Func<MethodDef, object, ArgumentList, Task>)InvokeMethod
                .MakeGenericMethod(tResult)
                .CreateDelegate(typeof(Func<MethodDef, object, ArgumentList, Task>)));
        return (service, arguments) => staticInvoker.Invoke(this, service, arguments);
    }

    private static Task InvokeAsync<TResult>(MethodDef methodDef, object service, ArgumentList arguments)
    {
        var result = arguments.GetInvoker(methodDef.Method).Invoke(service, arguments);
        if (methodDef.ReturnsTask) {
            var task = (Task)result!;
            if (methodDef.IsAsyncVoidMethod)
                return task.IsCompletedSuccessfully() ? TaskExt.UnitTask : ToUnitTask(task);
            return task;
        }

        if (methodDef.ReturnsValueTask) {
            if (result is ValueTask<TResult> valueTask)
                return valueTask.AsTask();
            if (result is ValueTask voidValueTask)
                return voidValueTask.IsCompletedSuccessfully ? TaskExt.UnitTask : ToUnitTask(voidValueTask);
        }

        return Task.FromResult((TResult)result!);
    }

    private static async Task<Unit> ToUnitTask(Task source)
    {
        await source.ConfigureAwait(false);
        return default;
    }

    private static async Task<Unit> ToUnitTask(ValueTask source)
    {
        await source.ConfigureAwait(false);
        return default;
    }
}
