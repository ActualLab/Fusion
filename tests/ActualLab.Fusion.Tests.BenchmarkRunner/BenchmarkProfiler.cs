using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

public static class BenchmarkProfiler
{
    [UnconditionalSuppressMessage("Trimming", "IL2080")]
    public static async Task Run(string selector, int durationSeconds)
    {
        var benchmark = FindBenchmark(selector);
        var constructor = benchmark.Type.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"'{benchmark.Type}' doesn't have a public parameterless constructor.");
        var instance = constructor.Invoke(null);
        var globalSetup = GetLifecycleMethods<GlobalSetupAttribute>(benchmark.Type);
        var globalCleanup = GetLifecycleMethods<GlobalCleanupAttribute>(benchmark.Type);
        var iterationSetup = GetLifecycleActions<IterationSetupAttribute>(benchmark.Type, instance);
        var iterationCleanup = GetLifecycleActions<IterationCleanupAttribute>(benchmark.Type, instance);
        var invokeBenchmark = CompileAction(benchmark.Method, instance);

        Console.WriteLine($"Profiling {benchmark.Type.Name}.{benchmark.Method.Name} for {durationSeconds} seconds.");
        try {
            await InvokeAll(globalSetup, instance).ConfigureAwait(false);
            RunCycle(iterationSetup, invokeBenchmark, iterationCleanup);

            var cycleCount = 0L;
            var stopwatch = Stopwatch.StartNew();
            do {
                RunCycle(iterationSetup, invokeBenchmark, iterationCleanup);
                cycleCount++;
            } while (stopwatch.Elapsed.TotalSeconds < durationSeconds);
            Console.WriteLine($"Completed {cycleCount:N0} cycles in {stopwatch.Elapsed.TotalSeconds:N2} seconds.");
        }
        finally {
            await InvokeAll(globalCleanup, instance).ConfigureAwait(false);
        }
    }

    private static (Type Type, MethodInfo Method) FindBenchmark(string selector)
    {
        var matches = typeof(BenchmarkProfiler).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => method.GetCustomAttribute<BenchmarkAttribute>() is not null)
                .Select(method => (Type: type, Method: method)))
            .Where(x => $"{x.Type.Name}.{x.Method.Name}".Contains(selector, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length switch {
            1 => matches[0],
            0 => throw new ArgumentOutOfRangeException(nameof(selector), $"No benchmark matches '{selector}'."),
            _ => throw new ArgumentOutOfRangeException(nameof(selector), $"More than one benchmark matches '{selector}'."),
        };
    }

    private static MethodInfo[] GetLifecycleMethods<TAttribute>(Type type)
        where TAttribute : Attribute
        => type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.GetCustomAttribute<TAttribute>() is not null)
            .ToArray();

    private static Action[] GetLifecycleActions<TAttribute>(Type type, object instance)
        where TAttribute : Attribute
        => GetLifecycleMethods<TAttribute>(type)
            .Select(method => CompileAction(method, instance))
            .ToArray();

    private static Action CompileAction(MethodInfo method, object instance)
    {
        var call = Expression.Call(Expression.Constant(instance), method);
        Expression body = method.ReturnType == typeof(void)
            ? call
            : Expression.Block(call, Expression.Empty());
        return Expression.Lambda<Action>(body).Compile();
    }

    private static void RunCycle(Action[] setup, Action benchmark, Action[] cleanup)
    {
        foreach (var action in setup)
            action();
        benchmark();
        foreach (var action in cleanup)
            action();
    }

    private static async Task InvokeAll(MethodInfo[] methods, object instance)
    {
        foreach (var method in methods) {
            var result = method.Invoke(instance, null);
            if (result is Task task)
                await task.ConfigureAwait(false);
            else if (result is ValueTask valueTask)
                await valueTask.ConfigureAwait(false);
        }
    }
}
