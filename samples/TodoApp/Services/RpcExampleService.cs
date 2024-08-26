using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

public class RpcExampleService : IRpcExampleService
{
    public Task<string> Greet(string name)
        => Task.FromResult($"Hello, {name}!");

    public Task<(RpcObjectId, string)> GetComplex()
        => Task.FromResult((new RpcObjectId(Guid.NewGuid(), 1), "Second item"));

    public Task<RpcStream<int>> GetStream(CancellationToken cancellationToken = default)
        => Task.FromResult(new RpcStream<int>(Enumerable.Range(0, int.MaxValue).ToAsyncEnumerable()));

    public Task<int> SumStream(RpcStream<int> stream, CancellationToken cancellationToken = default)
        => stream.SumAsync(cancellationToken).AsTask();
}
