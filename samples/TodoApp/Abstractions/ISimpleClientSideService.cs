using ActualLab.Rpc;

namespace Samples.TodoApp.Abstractions;

public interface ISimpleClientSideService : IRpcService
{
    // You aren't limited to RpcNoWait methods here - we use this kind of method just as an example
    public Task<RpcNoWait> Pong(string message);
}
