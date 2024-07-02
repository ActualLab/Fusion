using ActualLab.Rpc;

namespace ActualLab.Tests.Rpc;

public interface ITestRpcBackend : ICommandService, IBackendService
{
    Task<ITuple> Polymorph(ITuple argument, CancellationToken cancellationToken = default);
}

public interface ITestRpcBackendClient : ITestRpcBackend;

public class TestRpcBackend : ITestRpcBackend
{
    public virtual Task<ITuple> Polymorph(ITuple argument, CancellationToken cancellationToken = default)
        => Task.FromResult(argument);
}
