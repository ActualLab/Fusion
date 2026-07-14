namespace ActualLab.Fusion.Tests.Services;

public interface IServeStaleTester : IComputeService
{
    [ComputeMethod]
    public Task<string> Get(string key, CancellationToken cancellationToken = default);
}

public class ServeStaleTester : IServeStaleTester
{
    public virtual Task<string> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult("v-" + key);
}
