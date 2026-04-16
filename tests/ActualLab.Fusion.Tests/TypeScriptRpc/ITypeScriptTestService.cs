using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public interface ITypeScriptTestService : IRpcService
{
    Task<int> Add(int a, int b);
    Task<int> Add(int a, int b, int c);
    Task<string> Greet(string name);
    Task<bool> Negate(bool value);
    Task<double> Divide(double a, double b);
    Task<string?> Echo(string? message);

    /// <summary>Slow echo — increments an invocation counter, waits, then returns the marker.</summary>
    Task<string> SlowEcho(string marker, int delayMs);
    /// <summary>Current invocation count for SlowEcho — used by the $sys.Reconnect E2E scenario.</summary>
    Task<int> GetSlowEchoInvocationCount();
    /// <summary>Reset the invocation counter. Called at the start of each scenario.</summary>
    Task ResetSlowEchoCounter();
}
