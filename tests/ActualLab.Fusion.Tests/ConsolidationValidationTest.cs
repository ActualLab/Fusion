using ActualLab.Fusion.Client;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Testing;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests;

// ConsolidationDelay is honored by ComputeMethodFunction, i.e. by locally computed compute methods.
// Local and Server services always compute their methods that way, and a Client just consumes
// the value its producer already consolidated - so all of them are fine. A Distributed service is not:
// its methods are served by RemoteComputeMethodFunction even when the routing resolves to
// the local peer, and that one always produces a plain ComputeMethodComputed.

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class ConsolidationValidationTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private const string ExpectedMessage = "*ConsolidationDelay cannot be used on*Distributed service*";

    [Fact]
    public void LocalRegistrationIsAccepted()
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddService<IConsolidatingCounter, ConsolidatingCounter>(RpcServiceMode.Local);
        act.Should().NotThrow();
    }

    [Fact]
    public void ServerRegistrationIsAccepted()
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddServer<IConsolidatingCounter, ConsolidatingCounter>();
        act.Should().NotThrow();
    }

    [Fact]
    public void ClientRegistrationIsAccepted()
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddClient<IConsolidatingCounter>();
        act.Should().NotThrow();
    }

    [Fact]
    public void ServerAndClientRegistrationIsAccepted()
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddServerAndClient<IConsolidatingCounter, ConsolidatingCounter>();
        act.Should().NotThrow();
    }

    [Fact]
    public void DistributedRegistrationIsRejected()
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddDistributedService<IConsolidatingCounter, ConsolidatingCounter>();
        act.Should().Throw<InvalidOperationException>().WithMessage(ExpectedMessage);
    }

    [Fact]
    public void RemoteComputeMethodAttributeIsRejected()
    {
        // This one is rejected by ComputedOptions.Get, i.e. when the service proxy is built
        var act = () => CreateServices(s => s.AddFusion().AddClient<IRemoteConsolidatingCounter>())
            .GetRequiredService<IRemoteConsolidatingCounter>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConsolidationDelay cannot be used with RemoteComputeMethodAttribute*");
    }

    [Fact]
    public async Task ServerRegistrationConsolidates()
    {
        await using var services = CreateServices(
            s => s.AddFusion().AddServer<IConsolidatingCounter, ConsolidatingCounter>());
        var counter = (ConsolidatingCounter)services.GetRequiredService<IConsolidatingCounter>();
        counter.Set("a", 1);

        var c0 = (ConsolidatingComputed<int>)await Computed.Capture(() => counter.Get("a"));
        c0.Value.Should().Be(1);

        counter.Set("a", 1); // Same value -> the invalidation is swallowed
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeTrue();

        counter.Set("a", 2); // Different value -> the invalidation propagates
        await (c0.WhenConsolidated ?? Task.CompletedTask);
        c0.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task NoConsolidationDelayProducesPlainComputed()
    {
        await using var services = CreateServices(
            s => s.AddFusion().AddServer<IPlainCounter, PlainCounter>());
        var counter = services.GetRequiredService<IPlainCounter>();

        var computed = await Computed.Capture(() => counter.Get("abc"));
        computed.Should().BeOfType<ComputeMethodComputed<int>>();
        computed.Value.Should().Be(3);
    }

    [Fact]
    public async Task ProtectedConsolidationWorksOnLocallyHostedRpcService()
    {
        await using var services = CreateServices(s => {
            s.AddFusion().AddDistributedService<IHiddenConsolidatingCounter, HiddenConsolidatingCounter>();
            s.AddSingleton<RpcOutboundCallOptions>(_ => RpcOutboundCallOptions.Default with {
                RouterFactory = _ => static _ => RpcRef.Local,
            });
        });
        var counter = (HiddenConsolidatingCounter)services.GetRequiredService<IHiddenConsolidatingCounter>();
        await counter.Set("a", 1);

        // The RPC-exposed method is served by RemoteComputeMethodFunction even when it's computed locally
        var computed = await Computed.Capture(() => counter.Get("a"));
        computed.Should().BeOfType<ComputeMethodComputed<int>>();
        computed.Value.Should().Be(1);

        // ... but the protected compute method it derives from still consolidates
        var consolidated = (ConsolidatingComputed<int>)await counter.CaptureConsolidated("a");
        consolidated.Value.Should().Be(1);
        consolidated.Options.ConsolidationDelay.Should().Be(TimeSpan.Zero);

        await counter.Set("a", 1); // Same value -> no invalidation
        await (consolidated.WhenConsolidated ?? Task.CompletedTask);
        consolidated.IsConsistent().Should().BeTrue();
        computed.IsConsistent().Should().BeTrue();

        await counter.Set("a", 2); // Different value -> invalidation
        await (consolidated.WhenConsolidated ?? Task.CompletedTask);
        consolidated.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task ServerConsolidationSuppressesClientInvalidation()
    {
        await using var services = CreateServices(
            s => s.AddFusion().AddServerAndClient<IConsolidatingCounter, ConsolidatingCounter>());
        var server = (ConsolidatingCounter)services.GetRequiredService<IConsolidatingCounter>();
        var client = services.RpcHub().GetClient<IConsolidatingCounter>();
        var connection = services.GetRequiredService<RpcTestClient>().GetConnection(x => !x.IsBackend);
        await connection.ClientPeer.WhenConnected();
        server.Set("a", 1);

        // ConsolidationDelay is inert on the client - it just consumes the consolidated value
        var clientComputed = await Computed.Capture(() => client.Get("a"));
        clientComputed.Should().BeAssignableTo<IRemoteComputed>();
        clientComputed.Value.Should().Be(1);

        server.Set("a", 1); // Same value -> the server swallows its own invalidation
        await Delay(0.5);
        clientComputed.IsConsistent().Should().BeTrue(); // ... so the client never hears about it

        server.Set("a", 2); // Different value -> the invalidation reaches the client
        await clientComputed.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(5));
        clientComputed.IsConsistent().Should().BeFalse();
    }
}
