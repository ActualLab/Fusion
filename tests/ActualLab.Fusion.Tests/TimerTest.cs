using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

public class TimerTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        var fusion = services.AddFusion();
        if (!isClient)
            fusion.AddService<ITimeService, TimeService>();
        else
            fusion.AddClient<ITimeService>();
    }

    [Fact]
    public async Task BasicTest()
    {
        await using var serving = await WebHost.Serve();
        var tp = WebServices.GetRequiredService<ITimeService>();
        var ctp = ClientServices.GetRequiredService<ITimeService>();

        var cTime = await Computed.Capture(() => ctp.GetTime()).AsTask().WaitAsync(TimeSpan.FromMinutes(1));
        var count = 0;
        using var state = WebServices.StateFactory().NewComputed<DateTime>(
            FixedDelayer.NextTick,
            async ct => await ctp.GetTime(ct));
        state.Updated += (s, _) => {
            Out.WriteLine($"Client: {s.Value}");
            count++;
        };

        await TestExt.When(
            () => count.Should().BeGreaterThan(2),
            TimeSpan.FromSeconds(5));
    }
}
