using Microsoft.Extensions.Hosting;

namespace ActualLab.Async;

/// <summary>
/// Defines the contract for a long-running background worker that can be started and stopped.
/// </summary>
public interface IWorker : IAsyncDisposable, IDisposable, IHasWhenDisposed, IHostedService
{
    public Task? WhenRunning { get; }

    public Task Run();
    public Task Stop();
}
