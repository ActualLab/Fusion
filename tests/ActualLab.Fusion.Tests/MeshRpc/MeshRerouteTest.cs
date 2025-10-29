using ActualLab.Fusion.Testing;
using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.MeshRpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class MeshRerouteTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task BasicRerouteTest()
    {
        await using var testHosts = NewTestHosts();

        // Create 2 hosts and 1 client
        var host0 = testHosts.NewHost(RpcServiceMode.DistributedPair);
        var host1 = testHosts.NewHost(RpcServiceMode.DistributedPair);
        var client = testHosts.ClientHost;
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted, client.WhenStarted);
        Out.WriteLine($"Created hosts: {host0.Id}, {host1.Id}");
        Out.WriteLine($"Created client: {client.Id}");

        // Get the service from the client
        var service = client.Services.GetRequiredService<IRpcRerouteTestService>();

        // Initially set a value on host0
        var initialResult = await service.SetValue(
            new RpcRerouteTestService_SetValue(0, "test-key", "value-from-host0"));
        initialResult.HostId.Should().Be(host0.Id);
        Out.WriteLine($"Set value on {initialResult.HostId}");

        // Capture the computed from host0
        var computed = await Computed.Capture(() => service.GetValue(0, "test-key"));
        computed.Value.Value.Should().Be("value-from-host0");
        computed.Value.HostId.Should().Be(host0.Id);
        Out.WriteLine($"Initial computed from {computed.Value.HostId}: {computed.Value.Value}");

        // Swap the hosts - this should trigger rerouting
        Out.WriteLine("Swapping hosts...");
        testHosts.MeshMap.Swap(0, 1);

        // The computed should become inconsistent due to rerouting
        await ComputedTest.When(async ct => {
            var v = await computed.Use(ct);
            v.HostId.Should().Be(host1.Id, "after reroute, calls to index 0 should go to host1");
            v.Value.Should().Be("", "host1 should have empty storage initially");
        }, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RerouteWithValueChangeTest()
    {
        await using var testHosts = NewTestHosts();

        // Create 2 hosts and 1 client
        var host0 = testHosts.NewHost(RpcServiceMode.DistributedPair);
        var host1 = testHosts.NewHost(RpcServiceMode.DistributedPair);
        var client = testHosts.ClientHost;
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted, client.WhenStarted);
        Out.WriteLine($"Created hosts: {host0.Id}, {host1.Id}");

        var service = client.Services.GetRequiredService<IRpcRerouteTestService>();

        // Set initial value on host0
        await service.SetValue(
            new RpcRerouteTestService_SetValue(0, "key1", "host0-value"));

        // Set a different value on host1 for the same key
        await service.SetValue(
            new RpcRerouteTestService_SetValue(1, "key1", "host1-value"));

        // Capture computed from host0
        var computed = await Computed.Capture(() => service.GetValue(0, "key1"));
        computed.Value.Value.Should().Be("host0-value");
        computed.Value.HostId.Should().Be(host0.Id);
        Out.WriteLine($"Initial: {computed.Value.HostId} = {computed.Value.Value}");

        // Swap hosts
        Out.WriteLine("Swapping hosts...");
        testHosts.MeshMap.Swap(0, 1);

        // Wait for invalidation
        await ComputedTest.When(async ct => {
            var v = await computed.Use(ct);
            v.HostId.Should().Be(host1.Id);
            v.Value.Should().Be("host1-value");
        }, TimeSpan.FromSeconds(5));
    }

    // Private methods

    private static MeshHostSet NewTestHosts()
        => new((host, services) => {
            var fusion = services.AddFusion();
            fusion.AddService<IRpcRerouteTestService, RpcRerouteTestService>(host.ServiceMode);
        });
}
