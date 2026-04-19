using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

public class PushedComputeServiceTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task SetRoutesValueThroughStash()
    {
        var services = CreateServices();
        var svc = services.GetRequiredService<PushedComputeService>();

        (await svc.Get("a")).Should().Be(0);
        svc.StorageReadCount.Should().Be(1);

        await svc.Set("a", 5);
        (await svc.Get("a")).Should().Be(5);
        svc.StorageReadCount.Should().Be(1); // stash hit, no extra storage read
        svc.Pusher.Reservations.Count.Should().Be(0);      // stash entry consumed

        await svc.SetRaw("a", 7);
        (await svc.Get("a")).Should().Be(7);
        svc.StorageReadCount.Should().Be(2); // SetRaw bypasses stash
    }

    [Fact]
    public async Task ConcurrentSetsSerialize()
    {
        var services = CreateServices();
        var svc = services.GetRequiredService<PushedComputeService>();

        var tasks = Enumerable.Range(1, 20)
            .Select(i => svc.Set("k", i))
            .ToArray();
        await Task.WhenAll(tasks);

        var final = await svc.Get("k");
        final.Should().BeInRange(1, 20);
        svc.Pusher.Reservations.Count.Should().Be(0);
    }

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        services.AddFusion().AddService<PushedComputeService>();
    }
}
