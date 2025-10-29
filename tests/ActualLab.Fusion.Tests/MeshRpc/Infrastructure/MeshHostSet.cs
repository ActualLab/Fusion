using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.MeshRpc;

public sealed class MeshHostSet : IAsyncDisposable
{
    private readonly Action<MeshHost, IServiceCollection>? _configureServices;
    private readonly LazySlim<MeshHost> _clientHostLazy;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    public MeshMap MeshMap { get; } = new();
    public ImmutableList<MeshHost> Hosts { get; private set; } = ImmutableList<MeshHost>.Empty;
    public MeshHost ClientHost => _clientHostLazy.Value;

    public MeshHostSet(Action<MeshHost, IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
        _clientHostLazy = new LazySlim<MeshHost>(() => NewHost(RpcServiceMode.Client, false));
    }

    public ValueTask DisposeAsync()
    {
        var disposeTasks = new List<Task>();
        foreach (var host in Hosts)
            disposeTasks.Add(host.DisposeAsync().AsTask());
        if (_clientHostLazy.HasValue)
            disposeTasks.Add(ClientHost.DisposeAsync().AsTask());
        return Task.WhenAll(disposeTasks).ToValueTask();
    }

    public MeshHost NewHost(
        RpcServiceMode serviceMode,
        bool allowLocalRpcConnectionKind = true,
        bool addToHostMap = true)
    {
        lock (_lock) {
            var host = new MeshHost(MeshMap, serviceMode, allowLocalRpcConnectionKind, _configureServices);
            if (serviceMode == RpcServiceMode.Client)
                return host;

            Hosts = Hosts.Add(host);
            if (addToHostMap)
                MeshMap.Add(host);
            return host;
        }
    }
}
