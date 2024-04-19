using ActualLab.Fusion.Extensions;
using ActualLab.Fusion.Testing;
using Xunit.Sdk;

namespace ActualLab.Fusion.Tests.Testing;

public class ComputedTestTest(ITestOutputHelper @out) : FusionTestBase(@out)
{
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
            throw new InvalidCastException("Not ok.");
        }
        catch (FailException e) {
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
            }, TimeSpan.FromSeconds(1));
            throw new InvalidCastException("Not ok.");
        }
        catch (FailException e) {
            Out.WriteLine($"Expected error: {e}");
        }
    }

    [Fact]
    public async Task WhenTimeoutTest1()
    {
        try {
            await ComputedTest.When(async ct => {
                await Task.Delay(2000, ct);
                return 1;
            }, TimeSpan.FromSeconds(1));
            throw new InvalidCastException("Not ok.");
        }
        catch (TimeoutException e) {
            Out.WriteLine($"Expected error: {e}");
        }
    }

    [Fact]
    public async Task WhenTimeoutTest2()
    {
        try {
            await ComputedTest.When(async ct => {
                await Task.Delay(2000, ct);
            }, TimeSpan.FromSeconds(1));
            throw new InvalidCastException("Not ok.");
        }
        catch (TimeoutException e) {
            Out.WriteLine($"Expected error: {e}");
        }
    }
}
