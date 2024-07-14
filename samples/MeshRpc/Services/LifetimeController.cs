using ActualLab.Async;
using ActualLab.Time;
using Pastel;

namespace Samples.MeshRpc.Services;

public class LifetimeController(Host ownHost) : WorkerBase
{
    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var lifespan = HostFactorySettings.HostLifespan.Next();
        Console.WriteLine($"{ownHost}: started, will termintate in {lifespan.ToShortString()}".Pastel(ConsoleColor.Cyan));
        await Task.Delay(lifespan, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"{ownHost}: life is so boring...".Pastel(ConsoleColor.Magenta));
        ownHost.RequestStop();
    }
}
