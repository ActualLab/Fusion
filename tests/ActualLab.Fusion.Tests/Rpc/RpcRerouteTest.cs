using ActualLab.Fusion.Testing;
using ActualLab.Fusion.Tests.Rpc.Services;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class RpcRerouteTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task BasicRerouteTest()
    {
        await using var helper = new RpcRerouteTestHelper();

        // Create 2 hosts and 1 client
        var host0 = await helper.CreateDistributedPairHost(30001);
        var host1 = await helper.CreateDistributedPairHost(30002);
        var client = await helper.CreateClientHost();

        Out.WriteLine($"Created hosts: {host0.Id}, {host1.Id}");
        Out.WriteLine($"Created client: {client.Id}");

        // Get the service from the client
        var service = helper.GetService(client);

        // Initially set a value on host0
        var initialResult = await service.SetValue(
            new RpcRerouteTestService_SetValue(host0.Id, "test-key", "value-from-host0"),
            default);
        initialResult.HostId.Should().Be(host0.Id);
        Out.WriteLine($"Set value on {initialResult.HostId}");

        // Capture the computed from host0
        var computed = await Computed.Capture(() => service.GetValue(host0.Id, "test-key"));
        computed.Value.Value.Should().Be("value-from-host0");
        computed.Value.HostId.Should().Be(host0.Id);
        Out.WriteLine($"Initial computed from {computed.Value.HostId}: {computed.Value.Value}");

        // Swap the hosts - this should trigger rerouting
        Out.WriteLine($"Swapping hosts...");
        helper.SwapHosts(0, 1);

        // The computed should become inconsistent due to rerouting
        await ComputedTest.When(async _ => {
            await Task.CompletedTask;
            computed.IsConsistent().Should().BeFalse();
        }, TimeSpan.FromSeconds(5));
        Out.WriteLine($"Computed became inconsistent after reroute");

        // Recompute - should now go to host1 (which is now in host0's position)
        computed = await computed.Update();
        computed.Value.HostId.Should().Be(host1.Id, "after reroute, calls to host0.Id should go to host1");
        Out.WriteLine($"After reroute, computed from {computed.Value.HostId}");

        // Verify the value is empty on the new host (host1)
        computed.Value.Value.Should().Be("", "host1 should have empty storage initially");
    }

    [Fact]
    public async Task RerouteWithValueChangeTest()
    {
        await using var helper = new RpcRerouteTestHelper();

        // Create 2 hosts and 1 client
        var host0 = await helper.CreateDistributedPairHost(30003);
        var host1 = await helper.CreateDistributedPairHost(30004);
        var client = await helper.CreateClientHost();

        Out.WriteLine($"Created hosts: {host0.Id}, {host1.Id}");

        var service = helper.GetService(client);

        // Set initial value on host0
        await service.SetValue(
            new RpcRerouteTestService_SetValue(host0.Id, "key1", "host0-value"),
            default);

        // Set a different value on host1 for the same key
        await service.SetValue(
            new RpcRerouteTestService_SetValue(host1.Id, "key1", "host1-value"),
            default);

        // Capture computed from host0
        var computed = await Computed.Capture(() => service.GetValue(host0.Id, "key1"));
        computed.Value.Value.Should().Be("host0-value");
        computed.Value.HostId.Should().Be(host0.Id);
        Out.WriteLine($"Initial: {computed.Value.HostId} = {computed.Value.Value}");

        // Swap hosts
        Out.WriteLine("Swapping hosts...");
        helper.SwapHosts(0, 1);

        // Wait for invalidation
        await ComputedTest.When(async _ => {
            await Task.CompletedTask;
            computed.IsConsistent().Should().BeFalse();
        }, TimeSpan.FromSeconds(5));

        // Update should now return host1's value
        computed = await computed.Update();
        computed.Value.Value.Should().Be("host1-value");
        computed.Value.HostId.Should().Be(host1.Id);
        Out.WriteLine($"After reroute: {computed.Value.HostId} = {computed.Value.Value}");
    }
}
