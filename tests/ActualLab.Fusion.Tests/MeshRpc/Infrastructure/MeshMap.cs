using System.Collections.Concurrent;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.MeshRpc;

public sealed class MeshMap(StateFactory stateFactory)
{
    private readonly ConcurrentDictionary<int, LazySlim<int, MeshMap, ShardPeerRef>> _peerRefs = new();

    public MutableState<ImmutableList<MeshHost>> State { get; }
        = stateFactory.NewMutable(ImmutableList<MeshHost>.Empty);

    public MeshMap()
        : this(StateFactory.Default)
    { }

    // Add/Remove/Reset

    public void Add(MeshHost host)
    {
        if (!State.Value.Contains(host))
            State.Set(x => {
                var hosts = x.Value;
                if (!hosts.Contains(host))
                    hosts = hosts.Add(host);
                return hosts;
            });
    }

    public void Remove(MeshHost host)
    {
        if (State.Value.Contains(host))
            State.Set(x => x.Value.Remove(host));
    }

    public void Reset(params MeshHost[] hosts)
        => State.Set(_ => hosts.ToImmutableList());

    public void Reset(ImmutableList<MeshHost> hosts)
        => State.Set(_ => hosts);

    public void Swap(int index1, int index2)
        => State.Set(x => {
            var hosts = x.Value;
            if (hosts.Count < 1)
                return hosts;

            var h1 = hosts.GetHostByShardIndex(index1)!;
            var h2 = hosts.GetHostByShardIndex(index2)!;
            return h1 == h2
                ? hosts
                : hosts.SetItem(index1, h2).SetItem(index2, h1);
        });

    // Get/RemoveShardPeerRef

    public ShardPeerRef GetShardPeerRef(int shardKey)
    {
        var sw = new SpinWait();
        while (true) {
            var shardIndex = shardKey.PositiveModulo(ShardPeerRef.ShardCount);
            var peerRef = _peerRefs.GetOrAdd(shardIndex,
                static (shardKey, self, holder) => new(self, shardKey, holder),
                this);
            if (!peerRef.RouteState.IsRerouted())
                return peerRef;

            sw.SpinOnce();
        }
    }

    internal void RemoveShardPeerRef(int shardIndex, LazySlim<int, MeshMap, ShardPeerRef> entry)
        => _peerRefs.TryRemove(shardIndex, entry);
}
