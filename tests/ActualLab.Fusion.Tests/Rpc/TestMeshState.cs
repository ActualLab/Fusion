namespace ActualLab.Fusion.Tests.Rpc;

public sealed class TestMeshState
{
    private static long _lastVersion;

    public static MutableState<TestMeshState> State { get; } = StateFactory.Default.NewMutable(new TestMeshState());

    public long Version { get; } = Interlocked.Increment(ref _lastVersion);
    public ImmutableArray<TestHost> Hosts { get; }
    public IReadOnlyDictionary<string, TestHost> HostById { get; }

    public static void Register(TestHost host)
        => State.Set(x => x.Value.WithHost(host));

    public static void Unregister(TestHost host)
        => State.Set(x => x.Value.WithoutHost(host));

    public static void Clear()
        => State.Set(_ => new TestMeshState());

    public TestMeshState(IReadOnlyList<TestHost>? hosts = null)
    {
        hosts ??= [];
        Hosts = [..hosts.OrderBy(x => x.Id)];
        HostById = hosts.ToDictionary(h => h.Id, StringComparer.Ordinal);
    }

    public TestMeshState WithHost(TestHost host)
    {
        if (HostById.ContainsKey(host.Id))
            return this;

        var hosts = Hosts.ToList();
        hosts.Add(host);
        return new TestMeshState(hosts);
    }

    public TestMeshState WithoutHost(TestHost host)
    {
        if (!HostById.ContainsKey(host.Id))
            return this;

        var hosts = Hosts.ToList();
        hosts.Remove(host);
        return new TestMeshState(hosts);
    }
}
