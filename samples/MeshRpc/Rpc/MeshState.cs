using ActualLab.Scalability;
using Pastel;

namespace Samples.MeshRpc;

public sealed class MeshState
{
    private static long _lastVersion;

    public static MutableState<MeshState> State { get; }

    public long Version { get; } = Interlocked.Increment(ref _lastVersion);
    public Host[] Hosts { get; }
    public IReadOnlyDictionary<string, Host> HostById { get; }
    public ShardMap<Host> ShardMap { get; }

    public static void Register(Host host)
        => State.Set(x => x.Value.WithHost(host));

    public static void Unregister(Host host)
        => State.Set(x => x.Value.WithoutHost(host));

    static MeshState()
    {
        State = StateFactory.Default.NewMutable(new MeshState());
        _ = Task.Run(async () => {
            await foreach (var (state, _) in State.Computed.Changes(FixedDelayer.NoneUnsafe))
                Console.WriteLine(state.ShardMap.ToString().PastelBg(ConsoleColor.DarkBlue));
        });
    }

    public MeshState(IReadOnlyList<Host>? hosts = null)
    {
        hosts ??= [];
        Hosts = [..hosts.OrderBy(x => x.Id)];
        HostById = hosts.ToDictionary(h => h.Id, StringComparer.Ordinal);
        ShardMap = new ShardMap<Host>(MeshSettings.ShardCount, Hosts);
    }

    public Host? GetShardHost(ShardRef shardRef)
        => ShardMap.IsEmpty ? null : ShardMap[shardRef.Key];

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
