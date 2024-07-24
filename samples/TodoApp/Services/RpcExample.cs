using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using Templates.TodoApp.Abstractions;

namespace Templates.TodoApp.Services;

public class RpcExample : IRpcExample
{
    public Task<string> Greet(string name)
        => Task.FromResult($"Hello, {name}!");

    public Task<(RpcObjectId, string)> GetComplex()
        => Task.FromResult((new RpcObjectId(Guid.NewGuid(), 1), "Second item"));

    public Task<RpcStream<int>> GetStream(int count, CancellationToken cancellationToken = default)
        => Task.FromResult(new RpcStream<int>(Enumerable.Range(0, count).ToAsyncEnumerable()));

    public Task<int> SumStream(RpcStream<int> stream, CancellationToken cancellationToken = default)
        => stream.SumAsync(cancellationToken).AsTask();
}
