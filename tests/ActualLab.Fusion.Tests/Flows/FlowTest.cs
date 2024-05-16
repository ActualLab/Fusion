using ActualLab.Flows;

namespace ActualLab.Fusion.Tests.Flows;

public class FlowTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var flows = Services.GetRequiredService<IFlows>();
        var f0 = await flows.GetOrStart<TimerFlow>("1");
        Out.WriteLine(f0.ToString());

        var c0 = await Computed.Capture(() => flows.Get(f0.Id)).ConfigureAwait(false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var (f, _) in c0.Changes(cts.Token)) {
            if (f == null)
                break;

            Out.WriteLine(f.ToString());
        }
    }
}
