using ActualLab.Tests;

namespace ActualLab.Fusion.Tests;

public class ComputeMethodAttributeTest(ITestOutputHelper @out) : TestBase(@out)
{
    public class Service : IComputeService
    {
        [ComputeMethod(NonTransientErrorInvalidationDelay = 2.5)]
        public virtual Task<int> WithHorizon() => Task.FromResult(0);
        [ComputeMethod]
        public virtual Task<int> Default() => Task.FromResult(0);
    }

    [Fact]
    public void NonTransientErrorInvalidationDelayIsReadFromAttribute()
    {
        var withHorizon = ComputedOptions.Get(typeof(Service), typeof(Service).GetMethod(nameof(Service.WithHorizon))!)!;
        withHorizon.NonTransientErrorInvalidationDelay.Should().Be(TimeSpan.FromSeconds(2.5));

        var @default = ComputedOptions.Get(typeof(Service), typeof(Service).GetMethod(nameof(Service.Default))!)!;
        @default.NonTransientErrorInvalidationDelay.Should().Be(ComputedOptions.Default.NonTransientErrorInvalidationDelay);
    }
}
