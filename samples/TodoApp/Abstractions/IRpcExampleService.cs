using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace Samples.TodoApp.Abstractions;

public interface IRpcExampleService : IRpcService
{
    Task<string> Greet(string name);
    Task<(RpcObjectId, string)> GetComplex();
    Task<RpcStream<int>> GetStream(CancellationToken cancellationToken = default);
    Task<int> SumStream(RpcStream<int> stream, CancellationToken cancellationToken = default);
}
