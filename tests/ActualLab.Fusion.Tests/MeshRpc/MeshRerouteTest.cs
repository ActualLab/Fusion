using ActualLab.Fusion.Testing;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.MeshRpc;

public class MeshRerouteTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task BasicRerouteTest()
    {
        await using var testHosts = NewTestHosts();

        // Create 2 hosts and 1 client
        var host0 = testHosts.NewHost();
        var host1 = testHosts.NewHost();
        var client = testHosts.ClientHost;
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted, client.WhenStarted);
        Out.WriteLine($"Created hosts: {host0.Id}, {host1.Id}");
        Out.WriteLine($"Created client: {client.Id}");

        // Get tested services from the client
        var commander = client.Commander();
        var service = client.GetRequiredService<IRpcRerouteTestService>();

        // Initially set a value on host0
        var initialResult = await commander.Call(
            new RpcRerouteTestService_SetValue(0, "test-key", "value-from-host0"));
        initialResult.HostId.Should().Be(host0.Id);
        Out.WriteLine($"Set value on {initialResult.HostId}");

        // Capture the computed from host0
        var computed = await Computed.Capture(() => service.GetValue(0, "test-key"));
        computed.Value.Value.Should().Be("value-from-host0");
        computed.Value.HostId.Should().Be(host0.Id);
        Out.WriteLine($"Initial computed from {computed.Value.HostId}: {computed.Value.Value}");

        // Verify with direct call
        var directResult = await service.GetValueDirect(0, "test-key");
        directResult.HostId.Should().Be(host0.Id);
        directResult.Value.Should().Be("value-from-host0");
        Out.WriteLine($"Direct call confirmed: {directResult.HostId} = {directResult.Value}");

        // First swap: hosts 0 <-> 1
        Out.WriteLine("Swapping hosts (0 <-> 1)...");
        testHosts.MeshMap.Swap(0, 1);

        // The computed should update to point to host1
        await ComputedTest.When(async ct => {
            var v = await computed.Use(ct);
            v.HostId.Should().Be(host1.Id, "after first swap, calls to index 0 should go to host1");
            v.Value.Should().Be("", "host1 should have empty storage initially");
        }, TimeSpan.FromSeconds(5));
        Out.WriteLine($"After first swap, computed now points to {host1.Id}");

        // Verify with direct call after first swap
        directResult = await service.GetValueDirect(0, "test-key");
        directResult.HostId.Should().Be(host1.Id, "direct call should also route to host1");
        directResult.Value.Should().Be("");
        Out.WriteLine($"Direct call after first swap: {directResult.HostId} = {directResult.Value}");

        // Second swap: hosts 0 <-> 1 again (back to original)
        Out.WriteLine("Swapping hosts again (0 <-> 1)...");
        testHosts.MeshMap.Swap(0, 1);

        // The computed should update back to point to host0
        await ComputedTest.When(async ct => {
            var v = await computed.Use(ct);
            v.HostId.Should().Be(host0.Id, "after second swap, calls to index 0 should go back to host0");
            v.Value.Should().Be("value-from-host0", "host0 should still have the original value");
        }, TimeSpan.FromSeconds(5));
        Out.WriteLine($"After second swap, computed points back to {host0.Id}");

        // Verify with direct call after second swap
        directResult = await service.GetValueDirect(0, "test-key");
        directResult.HostId.Should().Be(host0.Id, "direct call should route back to host0");
        directResult.Value.Should().Be("value-from-host0");
        Out.WriteLine($"Direct call after second swap: {directResult.HostId} = {directResult.Value}");
    }

    [Fact]
    public async Task RerouteWithValueChangeTest()
    {
        await using var testHosts = NewTestHosts();

        // Create 2 hosts and 1 client
        var host0 = testHosts.NewHost();
        var host1 = testHosts.NewHost();
        var client = testHosts.ClientHost;
        await Task.WhenAll(host0.WhenStarted, host1.WhenStarted, client.WhenStarted);
        Out.WriteLine($"Created hosts: {host0.Id}, {host1.Id}");

        var commander = client.Commander();
        var service = client.GetRequiredService<IRpcRerouteTestService>();

        // Set the initial value on host0
        await commander.Call(
            new RpcRerouteTestService_SetValue(0, "key1", "host0-value"));

        // Set a different value on host1 for the same key
        await commander.Call(
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
