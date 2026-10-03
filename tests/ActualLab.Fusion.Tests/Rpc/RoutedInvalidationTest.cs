using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

// A routed invalidation is an Invalidate-call-type RPC call (RpcCallTypeIds.Invalidate):
// the receiving host invalidates its own computed instead of computing it.
[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RoutedInvalidationTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task BackendPeer_InvalidatesTheComputed()
    {
        await using var services = CreateServices(
            s => s.AddFusion().AddServerAndClient<ICounterService, CounterService>());
        var (client, computed) = await Setup(services);
        var peer = await GetPeer(services, isBackend: true);

        await RouteInvalidation(peer, client);

        computed.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task NonBackendPeer_IsRejected()
    {
        await using var services = CreateServices(
            s => s.AddFusion().AddServerAndClient<ICounterService, CounterService>());
        var (client, computed) = await Setup(services);
        var peer = await GetPeer(services, isBackend: false);

        // The specific error matters: anything that merely fails the call would hide the guard
        // being gone - and the call would otherwise succeed, since Get("a") is local to this host
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => RouteInvalidation(peer, client));
        error.Message.Should().Contain("Backend");

        // Cache-busting an arbitrary key is a capability the mesh has and a client doesn't
        computed.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task NonBackendPeer_CanStillCallTheMethod()
    {
        await using var services = CreateServices(
            s => s.AddFusion().AddServerAndClient<ICounterService, CounterService>());
        var (client, computed) = await Setup(services);
        var peer = await GetPeer(services, isBackend: false);

        // What the guard must not do: reject the ordinary calls that are a client's whole purpose
        using (new RpcOutboundCallSetup(peer).Activate())
            (await client.Get("a")).Should().Be(1);

        computed.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task WithoutTheHeader_TheCallIsAnOrdinaryOne()
    {
        await using var services = CreateServices(
            s => s.AddFusion().AddServerAndClient<ICounterService, CounterService>());
        var (client, computed) = await Setup(services);
        var peer = await GetPeer(services, isBackend: true);

        using (new RpcOutboundCallSetup(peer).Activate())
            (await client.Get("a")).Should().Be(1);

        computed.IsConsistent().Should().BeTrue();
    }

    // Private methods

    // What OperationCompletionHandler.ApplyRouted does, pinned to one peer
    private static async Task RouteInvalidation(RpcPeer peer, ICounterService client)
    {
        using var _1 = new ComputeContext(CallOptions.RouteInvalidation, new InvalidationSource("test")).Activate();
        using var _2 = new RpcOutboundCallSetup(peer).Activate();
        await client.Get("a");
    }

    private static async Task<RpcPeer> GetPeer(IServiceProvider services, bool isBackend)
    {
        var testClient = services.GetRequiredService<RpcTestClient>();
        var peer = testClient.GetConnection(x => x.IsBackend == isBackend).ClientPeer;
        await peer.WhenConnected();
        return peer;
    }

    // The server-side computed is what a routed invalidation has to reach
    private static async Task<(ICounterService Client, Computed<int> Computed)> Setup(IServiceProvider services)
    {
        var server = (CounterService)services.GetRequiredService<ICounterService>();
        await server.Set("a", 1);
        var computed = await Computed.Capture(() => server.Get("a"));
        computed.Value.Should().Be(1);
        computed.IsConsistent().Should().BeTrue();
        return (services.RpcHub().GetClient<ICounterService>(), computed);
    }
}
