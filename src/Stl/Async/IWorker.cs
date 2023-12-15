using Microsoft.Extensions.Hosting;

namespace ActualLab.Async;

public interface IWorker : IAsyncDisposable, IDisposable, IHasWhenDisposed, IHostedService
{
    Task? WhenRunning { get; }

    Task Run();
    Task Stop();
}
