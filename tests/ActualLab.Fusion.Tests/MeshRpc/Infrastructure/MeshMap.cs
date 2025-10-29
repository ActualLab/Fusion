using System.Collections.Concurrent;

namespace ActualLab.Fusion.Tests.MeshRpc;

public sealed class MeshMap(StateFactory stateFactory)
{
    private const int RouteKeyModulo = 2*3*4*5; // Won't break the modulo for up to 5 hosts
    private readonly ConcurrentDictionary<int, LazySlim<int, MeshMap, MeshPeerRef>> _peerRefs = new();

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

    public void Reset(params ImmutableList<MeshHost> hosts)
        => State.Set(_ => hosts);

    public void Swap(int index1, int index2)
        => State.Set(x => {
            var hosts = x.Value;
            if (hosts.Count < 1)
                return hosts;

            var h1 = hosts.GetHostByRouteKey(index1)!;
            var h2 = hosts.GetHostByRouteKey(index2)!;
            return h1 == h2
                ? hosts
                : hosts.SetItem(index1, h2).SetItem(index2, h1);
        });

    // Get/RemovePeerRef

    public MeshPeerRef GetPeerRef(int routeKey)
    {
        var sw = new SpinWait();
        while (true) {
            var peerRef = _peerRefs.GetOrAdd(routeKey.PositiveModulo(RouteKeyModulo),
                static (routeKey, self, lazy) => new(self, routeKey, lazy),
                this);
            if (!peerRef.IsRerouted)
                return peerRef;

            sw.SpinOnce();
        }
    }

    internal void RemovePeerRef(int routeKey, LazySlim<int, MeshMap, MeshPeerRef> lazy)
        => _peerRefs.TryRemove(routeKey, lazy);
}
