using ActualLab.Fusion.Extensions;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests;

public class ComputedStaticTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task Test1()
    {
        var services = Computed.DefaultServices;
        var time = services.GetRequiredService<IFusionTime>();

        var testState = services.StateFactory().NewMutable("");
        _ = Task.Run(async () => {
            for (var i = 0; i < 3; i++) {
                await Task.Delay(1000).ConfigureAwait(false);
                testState.Set(x => x + "x");
            }
        });

        var t0 = await time.Now() - TimeSpan.FromSeconds(0.001);
        var c = Computed.New(async ct => {
            var delta = await time.Now(TimeSpan.FromSeconds(0.5)).ConfigureAwait(false) - t0;
            var s = await testState.Use(ct).ConfigureAwait(false);
            Out.WriteLine($"* {delta.ToShortString()}, testState.Value = {s}");
            return (delta, s);
        });
        c.Value.s.Should().BeNull();
        c.Value.delta.Should().Be(TimeSpan.Zero);

        c = await c.Update();
        c = await c.When(x => x.delta > TimeSpan.FromSeconds(5));
        c.Value.s.Should().Be("xxx");
    }

    [Fact]
    public async Task Test2()
    {
        var services = Computed.DefaultServices;
        var time = services.GetRequiredService<IFusionTime>();

        var t0 = await time.Now();
        var c = Computed.New(TimeSpan.FromMilliseconds(1), async _ => await time.Now() - t0);
        c.Value.Should().Be(TimeSpan.FromMilliseconds(1));
        await c.When(x => x > TimeSpan.FromSeconds(2));
    }
}
