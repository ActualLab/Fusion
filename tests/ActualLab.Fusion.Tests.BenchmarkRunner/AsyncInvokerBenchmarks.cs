using System.Reflection;
using ActualLab.Interception;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

[MemoryDiagnoser]
public class AsyncInvokerBenchmarks
{
    private const int OperationCount = 65_536;
    private static readonly MethodInfo Method = typeof(AsyncInvokerBenchmarks)
        .GetMethod(nameof(GetCompleted), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly Func<ArgumentList, Task<Unit>> Intercepted = static _ => TaskExt.UnitTask;

    private readonly Invocation _invocation = new(new object(), Method, ArgumentList.New(), Intercepted);
    private readonly Func<Invocation, ValueTask<object?>> _invoker = new MethodDef(
        typeof(AsyncInvokerBenchmarks),
        Method).InterceptedObjectAsyncInvoker;

    [Benchmark(OperationsPerInvoke = OperationCount)]
    public ValueTask<object?> CompletedTask()
    {
        ValueTask<object?> result = default;
        for (var i = 0; i < OperationCount; i++)
            result = _invoker.Invoke(_invocation);
        return result;
    }

    private static Task<Unit> GetCompleted()
        => TaskExt.UnitTask;
}
