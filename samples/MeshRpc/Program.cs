using ActualLab.Generators;
using ActualLab.Mathematics;
using Pastel;
using Samples.MeshRpc;
using static Samples.MeshRpc.HostFactorySettings;
using Host = Samples.MeshRpc.Host;

var stopTokenSource = new CancellationTokenSource();
var stopToken = stopTokenSource.Token;
Console.CancelKeyPress += (_, e) => {
    Console.WriteLine("Got Ctrl-C, stopping...".Pastel(ConsoleColor.Yellow));
    stopTokenSource.Cancel();
    e.Cancel = true;
};

await AddHosts(stopToken).ConfigureAwait(false);

async Task AddHosts(CancellationToken cancellationToken = default)
{
    var clientHost = new Host(-1);
    clientHost.Start();

    var meshState = MeshState.State;
    var maxAddProbability = (MaxHostCount - MinHostCount) / MaxHostCount;
    try {
        while (true) {
            var hostCount = meshState.Value.Hosts.Length;
            var addProbability = (MaxHostCount - hostCount) / MaxHostCount;
            if (addProbability > maxAddProbability)
                addProbability = 1;
            if (Random.Shared.NextDouble() <= addProbability)
                AddHost();

            await Task.Delay(HostTryAddPeriod.Next(), cancellationToken).ConfigureAwait(false);
        }
    }
    finally {
        await clientHost.DisposeAsync().ConfigureAwait(false);
        while (true) {
            var hostCount = meshState.Value.Hosts.Length;
            if (hostCount == 0)
                break;

            Console.WriteLine($"Stopping hosts (remaining: {hostCount})");
            var host = meshState.Value.Hosts[0];
            await host.DisposeAsync().ConfigureAwait(false);
            await meshState.When(x => !x.HostById.ContainsKey(host.Id)).ConfigureAwait(false);
        }
    }
}

static void AddHost()
{
    var hosts = MeshState.State.Value.Hosts;
    var freePortSlots = Enumerable.Range(0, (int)MaxHostCount).Except(hosts.Select(h => h.PortSlot)).ToList();
    if (freePortSlots.Count <= 0)
        return;

    var freePortSlot = freePortSlots[RandomShared.Next().PositiveModulo(freePortSlots.Count)];
    var host = new Host(freePortSlot);
    host.Start();
}
