using ActualLab.Rpc;
using Microsoft.Extensions.Hosting;

namespace Samples.MeshRpc.Services;

public class MeshStateUpdater(IServiceProvider services) : WorkerBase
{
    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        var ownHost = services.GetRequiredService<Host>();
        if (ownHost.ServiceMode == RpcServiceMode.Client)
            return;

        var applicationLifetime = services.GetRequiredService<IHostApplicationLifetime>();
        try {
            await TaskExt.NeverEnding(applicationLifetime.ApplicationStarted).SilentAwait(false);
            if (cancellationToken.IsCancellationRequested)
                return;

            MeshState.Register(ownHost);
            await TaskExt.NeverEnding(applicationLifetime.ApplicationStopping).SilentAwait(false);
        }
        finally {
            // Console.WriteLine($"{ownHost}: unregistering...".Pastel(ConsoleColor.Yellow));
            MeshState.Unregister(ownHost);
        }
    }
}
