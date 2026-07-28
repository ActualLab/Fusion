using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;

namespace ActualLab.Tests.Rpc;

public class RpcBackendCommandGateTest(ITestOutputHelper @out) : RpcLocalTestBase(@out)
{
    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddCommander().AddService<TestBackendGateHandlers>();
        services.AddRpc().AddServerAndClient<ITestBackendGateService, TestBackendGateService>();
    }

    [Fact]
    public async Task BackendCommandParameterMakesMethodBackendOnly()
    {
        await using var services = CreateServices();
        var serviceDef = services.RpcHub().ServiceRegistry[typeof(ITestBackendGateService)];

        serviceDef.IsBackend.Should().BeFalse();
        serviceDef["OnBackend:2"].IsBackend.Should().BeTrue();
        serviceDef["OnAny:2"].IsBackend.Should().BeFalse();
    }

    [Fact]
    public async Task BackendCommandMethodIsRejectedOnNonBackendPeer()
    {
        await using var services = CreateServices();
        var frontPeer = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestBackendGateService>();
        await frontPeer.WhenConnected();

        using var _ = new RpcOutboundCallSetup(frontPeer).Activate();
        var result = await client.OnBackend(new BackendGate_Backend("x")).ResultAwait();

        result.Error.Should().BeOfType<RpcException>();
    }

    [Fact]
    public async Task DerivedBackendCommandIsRejectedOnNonBackendPeer()
    {
        await using var services = CreateServices();
        var testClient = services.GetRequiredService<RpcTestClient>();
        var frontPeer = testClient.GetConnection(x => !x.IsBackend).ClientPeer;
        var backendPeer = testClient.GetConnection(x => x.IsBackend).ClientPeer;
        var client = services.RpcHub().GetClient<ITestBackendGateService>();
        await frontPeer.WhenConnected();
        await backendPeer.WhenConnected();

        using (new RpcOutboundCallSetup(frontPeer).Activate())
            (await client.OnAny(new BackendGate_Public("ok"))).Should().Be("public:ok");
        using (new RpcOutboundCallSetup(backendPeer).Activate())
            (await client.OnAny(new BackendGate_DerivedBackend("ok"))).Should().Be("backend:ok");

        // OnAny declares the non-backend base type, so the method-level gate lets this through -
        // only the runtime-type check in RpcInboundCommandHandler can reject it.
        Result<string> result;
        using (new RpcOutboundCallSetup(frontPeer).Activate())
            result = await client.OnAny(new BackendGate_DerivedBackend("evil")).ResultAwait();

        result.Error.Should().BeOfType<NotSupportedException>();
    }
}
