using ActualLab.Interception;

namespace ActualLab.Tests.Interception;

public interface IProxyBenchmarkTester : IRequiresFullProxy
{
    void Void();
    Task Task(CancellationToken cancellationToken);
    ValueTask ValueTask(CancellationToken cancellationToken);

    int Int();
    int Int(int a);
    int Int(int a, int b);
    int IntFromObj(object a, object b);

    Task<int> IntTask(CancellationToken cancellationToken);
    Task<int> IntTask(int a, CancellationToken cancellationToken);
    Task<int> IntTask(int a, int b, CancellationToken cancellationToken);

    ValueTask<int> IntValueTask(CancellationToken cancellationToken);
    ValueTask<int> IntValueTask(int a, CancellationToken cancellationToken);
    ValueTask<int> IntValueTask(int a, int b, CancellationToken cancellationToken);

    Task<Unit> CommandLike(object command, CancellationToken cancellationToken);
}

public sealed class ProxyProxyBenchmarkTester : IProxyBenchmarkTester
{
    private static readonly Task<int> IntTaskResult = System.Threading.Tasks.Task.FromResult(0);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Void() { }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public Task Task(CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask ValueTask(CancellationToken cancellationToken) => default;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Int() => default;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Int(int a) => default;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int Int(int a, int b) => default;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int IntFromObj(object a, object b) => default;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public Task<int> IntTask(CancellationToken cancellationToken) => IntTaskResult;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public Task<int> IntTask(int a, CancellationToken cancellationToken) => IntTaskResult;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public Task<int> IntTask(int a, int b, CancellationToken cancellationToken) => IntTaskResult;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask<int> IntValueTask(CancellationToken cancellationToken) => default;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask<int> IntValueTask(int a, CancellationToken cancellationToken) => default;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public ValueTask<int> IntValueTask(int a, int b, CancellationToken cancellationToken) => default;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public Task<Unit> CommandLike(object command, CancellationToken cancellationToken) => TaskExt.UnitTask;
}
