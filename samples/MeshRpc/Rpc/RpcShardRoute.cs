using ActualLab.Rpc;
using Pastel;

namespace Samples.MeshRpc;

public sealed class RpcShardRoute : RpcRoute
{
    public string HostId { get; }

    public RpcShardRoute(RpcShardRef rpcRef)
        : base(rpcRef)
    {
        var meshState = MeshState.State.Value;
        HostId = meshState.GetShardHost(rpcRef.ShardRef)?.Id ?? "null";
        var initialExecutionDelayTask = Task.Delay(1000, ChangedToken).SuppressExceptions();
        LocalExecutionAwaiter =
            (_, ct) => initialExecutionDelayTask.IsCompleted
                ? default // Fast path
                : initialExecutionDelayTask.WaitAsync(ct).ToValueTask(); // Slow path
        _ = Task.Run(MarkChangedWhenHostChanges, CancellationToken.None);
        return;

        async Task MarkChangedWhenHostChanges() {
            Console.WriteLine($"{this}: created.".Pastel(ConsoleColor.Green));
            try {
                var computed = MeshState.State.Computed;
                if (HostId == "null")
                    await computed.When(x => x.Hosts.Length > 0, ChangedToken).ConfigureAwait(false);
                else
                    await computed.When(x => !x.HostById.ContainsKey(HostId), ChangedToken).ConfigureAwait(false);
                MarkChanged();
                Console.WriteLine($"{this}: rerouted.".Pastel(ConsoleColor.Yellow));
            }
            catch (OperationCanceledException) {
                // The route was marked as changed by other means
            }
        }
    }

    protected override string GetTargetString()
        => HostId;
}
