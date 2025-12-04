using ActualLab.Resilience;

namespace ActualLab.Fusion.Tests;

public class ComputedStateTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Theory]
    [InlineData(false, Transiency.NonTransient)]
    [InlineData(true, Transiency.NonTransient)]
    [InlineData(true, Transiency.Transient)]
    [InlineData(true, Transiency.SuperTransient)]
    public async Task BasicTest(bool computeSynchronously, Transiency errorTransiency)
    {
        var services = CreateServices(services => {
            services.AddTransiencyResolver<Computed>(_ => e => {
                var result = e is InvalidOperationException ? errorTransiency : Transiency.Unknown;
                return result.Or(e, TransiencyResolvers.PreferNonTransient);
            });
        });
        int count = 0;
        var s = services.StateFactory().NewComputed(
            new ComputedState<int>.Options() {
                InitialOutput = -1,
                TryComputeSynchronously = computeSynchronously,
            },
            _ => {
                var value = Interlocked.Increment(ref count);
                WriteLine($"Value: {value}");
                if (value == 3)
                    throw new InvalidOperationException("3");
                return Task.FromResult(value);
            });

        var c = s.Computed;
        if (computeSynchronously) {
            c.IsConsistent().Should().BeTrue();
            c.Value.Should().Be(1);
        }
        else {
            c.IsConsistent().Should().BeFalse();
            c.Value.Should().Be(-1);
        }

        (await s.Use()).Should().Be(1);
        c = s.Computed;
        c.IsConsistent().Should().BeTrue();
        c.Value.Should().Be(1);

        (await s.Use()).Should().Be(1);
        s.Computed.Should().BeSameAs(c);
        c.IsConsistent().Should().BeTrue();

        c.Invalidate();
        c.IsConsistent().Should().BeFalse();
        (await s.Use()).Should().Be(2);
        c = s.Computed;
        c.IsConsistent().Should().BeTrue();
        c.Value.Should().Be(2);

        (await s.Use()).Should().Be(2);
        s.Computed.Should().BeSameAs(c);
        c.IsConsistent().Should().BeTrue();

        c.Invalidate();
        c.IsConsistent().Should().BeFalse();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Use());
        c = s.Computed;
        c.IsConsistent().Should().BeTrue();
        c.Error!.Message.Should().Be("3");

        await Task.Delay(c.Options.TransientErrorInvalidationDelay.MultiplyBy(2));
        if (errorTransiency.IsAnyTransient()) {
            c.IsConsistent().Should().BeFalse();
            (await s.Use()).Should().Be(4);
        }
        else
            c.IsConsistent().Should().BeTrue();
    }
}
