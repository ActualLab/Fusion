using ActualLab.Rpc;
using Pastel;

namespace Samples.MeshRpc.Services;

public class LifetimeController(Host ownHost) : WorkerBase
{
    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var lifespan = HostFactorySettings.HostLifespan.Next();
        Console.WriteLine($"{ownHost}: started, will terminate in {lifespan.ToShortString()}".Pastel(ConsoleColor.Cyan));
        if (ownHost.ServiceMode == RpcServiceMode.Client)
            return; // No self-kill for the client

        await Task.Delay(lifespan, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"{ownHost}: x_x".Pastel(ConsoleColor.Magenta));
        ownHost.RequestStop();
    }
}
