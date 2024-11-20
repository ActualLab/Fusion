using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;

namespace ActualLab.DependencyInjection;

/// <summary>
/// Manages a group of <see cref="IHostedService"/>-s as a whole
/// allowing to start or stop all of them.
/// </summary>
public sealed class HostedServiceSet(IServiceProvider services)
    : IHasServices, IEnumerable<IHostedService>
{
    public static TimeSpan WarnTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public IServiceProvider Services { get; } = services;

    [field: AllowNull, MaybeNull]
    private ILogger Log => field ??= Services.LogFor(GetType());

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<IHostedService> GetEnumerator()
        // ReSharper disable once NotDisposedResourceIsReturned
        => Services.GetServices<IHostedService>().GetEnumerator();

    public async Task Start(CancellationToken cancellationToken = default)
    {
        var tasks = this.Select(s => s.StartAsync(cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        var services = Services.GetServices<IHostedService>().ToList();
        var tasks = services.Select(s => s.StopAsync(cancellationToken)).ToList();
        var whenStopped = Task.WhenAll(tasks);
        try {
            await whenStopped.WaitAsync(WarnTimeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException) {
            var failedServices = services.Zip(tasks, (s, t) => (Service: s, Task: t))
                .Where(x => !x.Task.IsCompleted)
                .Select(x => x.Service.GetType().GetName())
                .ToDelimitedString();
            Log.LogWarning("Hosted services failed to stop in time: {Services}", failedServices);
        }
        await whenStopped.ConfigureAwait(false);
    }
}
