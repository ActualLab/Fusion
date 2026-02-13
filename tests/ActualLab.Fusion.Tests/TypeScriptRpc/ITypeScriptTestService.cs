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
}
