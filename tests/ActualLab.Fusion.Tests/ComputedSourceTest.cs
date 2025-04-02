namespace ActualLab.Fusion.Tests;

public class ComputedSourceTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var services = CreateServices();

        var id = 0;
        var cs = new ComputedSource<int>(services,
            -1,
            (_, _) => {
                var value = Interlocked.Increment(ref id);
                Out.WriteLine($"Computed: {value}");
                return Task.FromResult(value);
            });

        cs.Computed.IsConsistent().Should().BeFalse();
        cs.Computed.Value.Should().Be(-1);

        (await cs.Use()).Should().Be(1);
        cs.Computed.IsConsistent().Should().BeTrue();
        cs.Computed.Value.Should().Be(1);

        (await cs.Use()).Should().Be(1);
        cs.Computed.IsConsistent().Should().BeTrue();
        cs.Computed.Value.Should().Be(1);

        (await cs.Computed.Use()).Should().Be(1);
        cs.Computed.Invalidate();

        (await cs.Use()).Should().Be(2);
        (await cs.Use()).Should().Be(2);
        cs.Computed.Value.Should().Be(2);
        (await cs.Computed.Use()).Should().Be(2);
    }

    [Fact]
    public async Task ComputedOptionsTest()
    {
        var services = CreateServices();

        var id = 0;
        var cs = new ComputedSource<int>(services,
            (_, _) => {
                var value = Interlocked.Increment(ref id);
                Out.WriteLine($"Computed: {value}");
                return Task.FromResult(value);
            }) {
            ComputedOptions = new() {
                AutoInvalidationDelay = TimeSpan.FromSeconds(0.2),
            }
        };
        cs.Computed.IsConsistent().Should().BeFalse();

        (await cs.Use()).Should().Be(1);
        cs.Computed.IsConsistent().Should().BeTrue();

        await cs.Computed.When(x => x > 1).WaitAsync(TimeSpan.FromSeconds(1));
        await cs.Computed.Changes().Take(3).CountAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        (await cs.Use()).Should().BeGreaterThan(3);
    }
}
