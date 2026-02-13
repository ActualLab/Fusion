namespace ActualLab.Fusion.Tests.TypeScriptRpc;

public interface ITypeScriptTestComputeService : IComputeService
{
    [ComputeMethod]
    Task<int> GetCounter(string key, CancellationToken cancellationToken = default);
    Task Set(string key, int value, CancellationToken cancellationToken = default);
    Task Increment(string key, CancellationToken cancellationToken = default);
}
