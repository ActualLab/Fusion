using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Testing;

namespace ActualLab.Fusion.Tests.Testing;

public class ComputedTestTest : FusionTestBase
{
    public ComputedTestTest(ITestOutputHelper @out) : base(@out)
        => UseTestClock = true;

    [Fact]
    public async Task WhenTest1()
    {
        var time = Services.GetRequiredService<IFusionTime>();
        var t0 = await time.Now();
        var d1 = await ComputedTest.When(async _ => {
            var delta = await time.Now(TimeSpan.FromSeconds(0.1)) - t0;
            Out.WriteLine($"* {delta.ToShortString()}");
            delta.Should().BeGreaterThan(TimeSpan.FromSeconds(1));
            return delta;
        });
        Out.WriteLine(d1.ToShortString());
    }

    [Fact]
    public async Task WhenTest2()
    {
        var time = Services.GetRequiredService<IFusionTime>();
        var t0 = await time.Now();
        await ComputedTest.When(async _ => {
            var delta = await time.Now(TimeSpan.FromSeconds(0.1)) - t0;
            Out.WriteLine($"* {delta.ToShortString()}");
            delta.Should().BeGreaterThan(TimeSpan.FromSeconds(1));
        });
    }

    [Fact]
    public async Task WhenFailTest1()
    {
        var time = Services.GetRequiredService<IFusionTime>();

        var t0 = await time.Now();
        try {
            await ComputedTest.When(async _ => {
                var delta = await time.Now(TimeSpan.FromSeconds(0.1)) - t0;
                Out.WriteLine($"* {delta.ToShortString()}");
                Assert.Fail("Ok");
                return delta;
            }, TimeSpan.FromSeconds(1));
            Assert.Fail();
        }
        catch (TimeoutException e) {
            Out.WriteLine($"Expected error: {e}");
        }
    }

    [Fact]
    public async Task WhenFailTest2()
    {
        var time = Services.GetRequiredService<IFusionTime>();
        var t0 = await time.Now();
        try {
            await ComputedTest.When(async _ => {
                var delta = await time.Now(TimeSpan.FromSeconds(0.1)) - t0;
                Out.WriteLine($"* {delta.ToShortString()}");
                Assert.Fail("Ok");
                return delta;
            }, TimeSpan.FromSeconds(1));
            Assert.Fail();
        }
        catch (TimeoutException e) {
            Out.WriteLine($"Expected error: {e}");
        }
    }
}
