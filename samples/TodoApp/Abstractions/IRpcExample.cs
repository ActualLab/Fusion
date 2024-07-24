using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace Templates.TodoApp.Abstractions;

public interface IRpcExample : IRpcService
{
    Task<string> Greet(string name);
    Task<(RpcObjectId, string)> GetComplex();
    Task<RpcStream<int>> GetStream(int count, CancellationToken cancellationToken = default);
    Task<int> SumStream(RpcStream<int> stream, CancellationToken cancellationToken = default);
}
