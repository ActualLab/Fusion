namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public class TypeScriptTestService : ITypeScriptTestService
{
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
}
