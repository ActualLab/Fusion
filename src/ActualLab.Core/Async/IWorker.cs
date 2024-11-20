using Microsoft.Extensions.Hosting;

namespace ActualLab.Async;

public interface IWorker : IAsyncDisposable, IDisposable, IHasWhenDisposed, IHostedService
{
    public Task? WhenRunning { get; }

    public Task Run();
    public Task Stop();
}
