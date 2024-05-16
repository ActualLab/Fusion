using ActualLab.Flows;
using ActualLab.Fusion.Testing;

namespace ActualLab.Fusion.Tests.Flows;

public class FlowTest : FusionTestBase
{
    public FlowTest(ITestOutputHelper @out) : base(@out)
        => DbType = FusionTestDbType.InMemory;

    [Fact]
    public async Task TimerFlowTest()
    {
        var flows = Services.GetRequiredService<IFlows>();
        var f0 = await flows.GetOrStart<TimerFlow>("1");
        Out.WriteLine($"[+] {f0}");

        await ComputedTest.When(async ct => {
            var flow = await flows.Get(f0.Id, ct);
            Out.WriteLine($"[*] {flow?.ToString() ?? "null"}");
            flow.Should().BeNull();
        }, TimeSpan.FromSeconds(30));
        Out.WriteLine($"[-] {f0.Id}");
    }
}
