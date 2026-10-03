using ActualLab.Fusion.Operations.Internal;
using ActualLab.Interception;
using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Tests.MeshRpc;

// What InvalidationMode.Distributed buys over Replicated: the invalidation is paid for once, on
// the host that owns the value. Here that host is a different process-like MeshHost reached over a
// real WebSocket connection, so this covers the whole path - capture, route, inbound Invalidate call.
public class DistributedInvalidationRoutingTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task RoutedInvalidation_ReachesTheHostThatOwnsTheValue()
    {
        await using var testHosts = NewTestHosts();
        var host0 = testHosts.NewHost();
        var host1 = testHosts.NewHost();
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted);

        // Shard 1 maps to hosts[1], i.e. everything keyed by it lives on host1
        const int shardKey = 1;
        var owner = host1.GetRequiredService<IRpcRerouteTestService>();
        await host1.Commander().Call(new RpcRerouteTestService_SetValue(shardKey, "k", "v", 0));
        var computed = await Computed.Capture(() => owner.GetValue(shardKey, "k"));
        computed.Value.HostId.Should().Be(host1.Id);
        computed.Value.Value.Should().Be("v");
        computed.IsConsistent().Should().BeTrue();

        var records = await CollectInvalidationCalls(host0, shardKey, "k");
        records.Count.Should().Be(1);
        records[0].MethodName.Should().Be("GetValue:3");

        await host0.GetRequiredService<FusionOperationCompletionHandler>()
            .ApplyRoutedInvalidations(records);

        computed.IsConsistent().Should().BeFalse();
    }

    [Fact]
    public async Task RoutedInvalidation_LeavesOtherKeysAlone()
    {
        await using var testHosts = NewTestHosts();
        var host0 = testHosts.NewHost();
        var host1 = testHosts.NewHost();
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted);

        const int shardKey = 1;
        var owner = host1.GetRequiredService<IRpcRerouteTestService>();
        await host1.Commander().Call(new RpcRerouteTestService_SetValue(shardKey, "k", "v", 0));
        await host1.Commander().Call(new RpcRerouteTestService_SetValue(shardKey, "other", "v", 0));
        var cInvalidated = await Computed.Capture(() => owner.GetValue(shardKey, "k"));
        var cKept = await Computed.Capture(() => owner.GetValue(shardKey, "other"));

        var records = await CollectInvalidationCalls(host0, shardKey, "k");
        await host0.GetRequiredService<FusionOperationCompletionHandler>()
            .ApplyRoutedInvalidations(records);

        cInvalidated.IsConsistent().Should().BeFalse();
        // A routed invalidation is as precise as a local one - it carries the arguments, not a shard
        cKept.IsConsistent().Should().BeTrue();
    }

    // Private methods

    // What a Distributed command handler's Invalidation.Defer(...) block ends up as
    private static async Task<ImmutableList<ServiceCall>> CollectInvalidationCalls(MeshHost host, int shardKey, string key)
    {
        var service = host.GetRequiredService<IRpcRerouteTestService>();
        var context = new DeferredInvalidationContext {
            Mode = DeferredInvalidationMode.Distributed,
        };
        using (context.Activate())
            Invalidation.Defer(() => _ = service.GetValue(shardKey, key, default));

        return await context.CollectInvalidationCalls();
    }

    private static MeshHostSet NewTestHosts()
        => new((host, services) => {
            var fusion = services.AddFusion();
            fusion.AddService<IRpcRerouteTestService, RpcRerouteTestService>(host.ServiceMode);
        });
}
