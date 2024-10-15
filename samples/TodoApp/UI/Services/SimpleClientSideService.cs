using ActualLab.Rpc;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.UI.Services;

public class SimpleClientSideService : ISimpleClientSideService
{
    public Channel<string> PongChannel { get; } = Channel.CreateUnbounded<string>();

    public Task<RpcNoWait> Pong(string message)
    {
        _ = PongChannel.Writer.WriteAsync(message);
        return RpcNoWait.Tasks.Completed;
    }
}
