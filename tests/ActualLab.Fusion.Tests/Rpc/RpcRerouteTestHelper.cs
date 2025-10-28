using ActualLab.Fusion.Tests.Rpc.Services;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.Rpc;

public sealed class RpcRerouteTestHelper : IAsyncDisposable
{
    private readonly List<TestHost> _hosts = new();
    private TestHost? _clientHost;

    public IReadOnlyList<TestHost> Hosts => _hosts;
    public TestHost? ClientHost => _clientHost;

    public async Task<TestHost> CreateHost(int port, RpcServiceMode serviceMode)
    {
        var host = new TestHost(port, serviceMode, services => {
            var fusion = services.AddFusion();
            fusion.AddService<IRpcRerouteTestService, RpcRerouteTestService>(serviceMode);
            fusion.Commander.AddHandlers<IRpcRerouteTestService>();
        });

        _hosts.Add(host);
        await host.Start();
        return host;
    }

    public async Task<TestHost> CreateClientHost()
    {
        if (_clientHost != null)
            throw new InvalidOperationException("Client host already created");

        _clientHost = await CreateHost(-1, RpcServiceMode.Client);
        return _clientHost;
    }

    public async Task<TestHost> CreateDistributedPairHost(int port)
        => await CreateHost(port, RpcServiceMode.DistributedPair);

    public async Task<TestHost> CreateDistributedHost(int port)
        => await CreateHost(port, RpcServiceMode.Distributed);

    public IRpcRerouteTestService GetService(TestHost? host = null)
    {
        host ??= _clientHost ?? _hosts.FirstOrDefault()
            ?? throw new InvalidOperationException("No hosts available");
        return host.Services.GetRequiredService<IRpcRerouteTestService>();
    }

    public void SwapHosts(int index1, int index2)
    {
        if (index1 < 0 || index1 >= _hosts.Count)
            throw new ArgumentOutOfRangeException(nameof(index1));
        if (index2 < 0 || index2 >= _hosts.Count)
            throw new ArgumentOutOfRangeException(nameof(index2));

        // Swap the hosts in the mesh state by unregistering and re-registering them
        var host1 = _hosts[index1];
        var host2 = _hosts[index2];

        // Temporarily store original IDs
        var originalId1 = host1.Id;
        var originalId2 = host2.Id;

        // Unregister both
        TestMeshState.Unregister(host1);
        TestMeshState.Unregister(host2);

        // Swap the list
        _hosts[index1] = host2;
        _hosts[index2] = host1;

        // Re-register with swapped positions
        TestMeshState.Register(host1);
        TestMeshState.Register(host2);
    }

    public async ValueTask DisposeAsync()
    {
        TestMeshState.Clear();

        foreach (var host in _hosts)
            await host.DisposeAsync();

        if (_clientHost != null)
            await _clientHost.DisposeAsync();

        _hosts.Clear();
        _clientHost = null;
    }
}
