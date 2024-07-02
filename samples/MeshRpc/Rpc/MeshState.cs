using ActualLab.Fusion;
using ActualLab.Mathematics;
using ActualLab.Text;

namespace Samples.MeshRpc;

public sealed class MeshState
{
    public static MutableState<MeshState> State { get; }
        = StateFactory.Default.NewMutable(new MeshState());

    public IReadOnlyList<Host> Hosts { get; }
    public IReadOnlyDictionary<Symbol, Host> HostById { get; }
    public HashRing<Host> HashRing { get; }

    public static void Register(Host host)
        => State.Set(x => x.Value.WithHost(host));

    public static void Unregister(Host host)
        => State.Set(x => x.Value.WithoutHost(host));

    public MeshState(IReadOnlyList<Host>? hosts = null)
    {
        hosts ??= Array.Empty<Host>();
        Hosts = hosts.OrderBy(x => x.Id).ToArray();
        HostById = hosts.ToDictionary(h => h.Id);
        HashRing = new HashRing<Host>(hosts, h => h.Hash);
    }

    public Host GetShardHost(ShardRef shardRef)
        => HashRing.FindNode(shardRef.Key * 1_299_709);

    public MeshState WithHost(Host host)
    {
        if (HostById.ContainsKey(host.Id))
            return this;

        var hosts = Hosts.ToList();
        hosts.Add(host);
        return new MeshState(hosts);
    }

    public MeshState WithoutHost(Host host)
    {
        if (!HostById.ContainsKey(host.Id))
            return this;

        var hosts = Hosts.ToList();
        hosts.Remove(host);
        return new MeshState(hosts);
    }
}
