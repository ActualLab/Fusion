using ActualLab.Async;
using ActualLab.Fusion;
using ActualLab.Generators;
using ActualLab.Mathematics;
using ActualLab.Time;
using Samples.MeshRpc;
using Host = Samples.MeshRpc.Host;

const int maxHostCount = 20;

var stopTokenSource = new CancellationTokenSource();
var stopToken = stopTokenSource.Token;
Console.CancelKeyPress += (_, e) => {
    Console.WriteLine("Got Ctrl-C, stopping...");
    stopTokenSource.Cancel();
    e.Cancel = true;
};

await AddHosts(stopToken).ConfigureAwait(false);

async Task AddHosts(CancellationToken cancellationToken = default)
{
    var actionPeriod = TimeSpan.FromSeconds(1).ToRandom(0.5);
    var meshState = MeshState.State;
    try {
        while (true) {
            var hostCount = meshState.Value.Hosts.Count;
            var addProbability = (maxHostCount - hostCount) / (double)maxHostCount;
            if (addProbability > 0.7)
                addProbability = 1;
            if (Random.Shared.NextDouble() < addProbability)
                AddHost();

            await Task.Delay(actionPeriod.Next(), cancellationToken).ConfigureAwait(false);
        }
    }
    finally {
        while (true) {
            var hostCount = meshState.Value.Hosts.Count;
            if (hostCount == 0)
                break;

            Console.WriteLine($"Stopping hosts (remaining: {hostCount})");
            var host = meshState.Value.Hosts[0];
            await host.DisposeAsync().ConfigureAwait(false);
            await meshState.When(x => !x.HostById.ContainsKey(host.Id)).ConfigureAwait(false);
        }
    }

}

static Host? AddHost()
{
    var hosts = MeshState.State.Value.Hosts;
    var freePortOffsets = Enumerable.Range(0, maxHostCount).Except(hosts.Select(h => h.PortOffset)).ToList();
    if (freePortOffsets.Count < 0)
        return null;

    var freePortOffset = freePortOffsets[ThreadRandom.Next().PositiveModulo(freePortOffsets.Count)];
    var host = new Host(freePortOffset);
    Console.WriteLine($"Starting host: '{host.Id}'");
    host.Start();
    return host;
}
