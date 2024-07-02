using ActualLab.Async;
using ActualLab.Time;
using Pastel;

namespace Samples.MeshRpc.Services;

public class HostKiller(Host ownHost) : WorkerBase
{
    private static readonly RandomTimeSpan Lifespan = TimeSpan.FromSeconds(10).ToRandom(0.75);

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var lifespan = Lifespan.Next();
        await Task.Delay(lifespan, cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"{ownHost.Id}: life is so boring...".Pastel(ConsoleColor.Magenta));
        ownHost.RequestStop();
    }
}
