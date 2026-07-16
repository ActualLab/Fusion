using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Serialization;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

[MemoryDiagnoser]
public abstract class RpcBenchmarkBase
{
    private ServiceProvider? _services;

    protected BenchmarkRpcPeer Peer { get; private set; } = null!;
    protected RpcMethodDef GetMethodDef { get; private set; } = null!;
    protected RpcMethodDef OkMethodDef { get; private set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddRpc().AddServerAndClient<IRpcBenchmarkService, RpcBenchmarkService>();
        _services = services.BuildServiceProvider();

        var hub = _services.RpcHub();
        var peerRef = RpcPeerRef.NewServer(
            "benchmark-peer",
            BenchmarkSettings.RpcSerializationFormat,
            isBackend: false);
        Peer = new BenchmarkRpcPeer(hub, peerRef);
        GetMethodDef = hub.ServiceRegistry[typeof(IRpcBenchmarkService)].Methods
            .Single(x => x.MethodInfo.Name == nameof(IRpcBenchmarkService.Get));
        OkMethodDef = hub.ServiceRegistry[typeof(IRpcSystemCalls)].Methods
            .Single(x => x.MethodInfo.Name == nameof(IRpcSystemCalls.Ok));
        OnSetup();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        OnCleanup();
        await Peer.DisposeAsync().ConfigureAwait(false);
        if (_services is not null)
            await _services.DisposeAsync().ConfigureAwait(false);
    }

    protected virtual void OnSetup() { }
    protected virtual void OnCleanup() { }

    protected ReadOnlyMemory<byte> Serialize(ArgumentList arguments, bool needsPolymorphism)
    {
        var buffer = RpcArgumentSerializer.GetWriteBuffer();
        Peer.ArgumentSerializer.Serialize(arguments, needsPolymorphism, buffer);
        return RpcArgumentSerializer.GetWriteBufferMemory(buffer);
    }

    protected RpcOutboundCall PrepareOutboundCall(long key)
    {
        var context = new RpcOutboundContext(Peer);
        var arguments = ArgumentList.New<long, CancellationToken>(key, default);
        var call = context.PrepareCall(GetMethodDef, arguments)!;
        call.Register();
        return call;
    }
}

public sealed class BenchmarkRpcPeer(RpcHub hub, RpcPeerRef peerRef) : RpcServerPeer(hub, peerRef)
{
    public RpcInboundContext? Dispatch(RpcInboundMessage message)
        => ProcessMessage(message, default, default);
}
