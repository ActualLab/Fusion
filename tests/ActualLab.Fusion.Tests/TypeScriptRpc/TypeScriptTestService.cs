namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class TypeScriptTestService : ITypeScriptTestService
{
    private static int _slowEchoInvocationCount;

    public Task<int> Add(int a, int b)
        => Task.FromResult(a + b);

    public Task<int> Add(int a, int b, int c)
        => Task.FromResult(a + b + c);

    public Task<string> Greet(string name)
        => Task.FromResult($"Hello, {name}!");

    public Task<bool> Negate(bool value)
        => Task.FromResult(!value);

    public Task<double> Divide(double a, double b)
        => Task.FromResult(a / b);

    public Task<string?> Echo(string? message)
        => Task.FromResult(message);

    public async Task<string> SlowEcho(string marker, int delayMs)
    {
        // Increment BEFORE the await so the client can observe the invocation
        // count before the handler returns.
        Interlocked.Increment(ref _slowEchoInvocationCount);
        await Task.Delay(delayMs).ConfigureAwait(false);
        return marker;
    }

    public Task<int> GetSlowEchoInvocationCount()
        => Task.FromResult(Volatile.Read(ref _slowEchoInvocationCount));

    public Task ResetSlowEchoCounter()
    {
        Volatile.Write(ref _slowEchoInvocationCount, 0);
        return Task.CompletedTask;
    }
}
