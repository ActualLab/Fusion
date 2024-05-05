namespace ActualLab.Fusion.Tests;

public class AnonymousComputedTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var services = CreateServices();

        var id = 0;
        var ci = new AnonymousComputedSource<int>(services,
            -1,
            (_, _) => {
                var value = Interlocked.Increment(ref id);
                Out.WriteLine($"Computed: {value}");
                return new ValueTask<int>(value);
            });

        ci.Computed.IsConsistent().Should().BeFalse();
        ci.Computed.Value.Should().Be(-1);

        (await ci.Use()).Should().Be(1);
        ci.Computed.IsConsistent().Should().BeTrue();
        ci.Computed.Value.Should().Be(1);

        (await ci.Use()).Should().Be(1);
        ci.Computed.IsConsistent().Should().BeTrue();
        ci.Computed.Value.Should().Be(1);

        (await ci.Computed.Use()).Should().Be(1);
        ci.Computed.Invalidate();

        (await ci.Use()).Should().Be(2);
        (await ci.Use()).Should().Be(2);
        ci.Computed.Value.Should().Be(2);
        (await ci.Computed.Use()).Should().Be(2);
    }

    [Fact]
    public async Task ComputedOptionsTest()
    {
        var services = CreateServices();

        var id = 0;
        var ci = new AnonymousComputedSource<int>(services,
            (_, _) => {
                var value = Interlocked.Increment(ref id);
                Out.WriteLine($"Computed: {value}");
                return new ValueTask<int>(value);
            }) {
            ComputedOptions = new() {
                AutoInvalidationDelay = TimeSpan.FromSeconds(0.2),
            }
        };
        ci.Computed.IsConsistent().Should().BeFalse();

        (await ci.Use()).Should().Be(1);
        ci.Computed.IsConsistent().Should().BeTrue();

        await ci.When(x => x > 1).WaitAsync(TimeSpan.FromSeconds(1));
        await ci.Changes().Take(3).CountAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        (await ci.Use()).Should().BeGreaterThan(3);
    }
}
