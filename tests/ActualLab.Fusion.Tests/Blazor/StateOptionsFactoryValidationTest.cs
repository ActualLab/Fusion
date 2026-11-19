using ActualLab.Fusion.Blazor;

namespace ActualLab.Fusion.Tests.Blazor;

public class StateOptionsFactoryValidationTest
{
    private readonly string _category = "Captured";

    [Fact]
    public void CaptureFreeFactoriesShouldPass()
    {
        ComputedStateComponent.ValidateStateOptionsFactory(typeof(ProbeComponent),
            static t => new ComputedState<int>.Options() { Category = t.Name });
        ComputedStateComponent.ValidateStateOptionsFactory(typeof(ProbeComponent),
            t => new ComputedState<int>.Options() { Category = t.Name });
        ComputedStateComponent.ValidateStateOptionsFactory(typeof(ProbeComponent), CreateOptions);
    }

    [Fact]
    public void CapturingFactoriesShouldThrow()
    {
        var category = "Local";
        Func<Type, IComputedStateOptions>[] factories = [
            _ => new ComputedState<int>.Options() { Category = _category },
            _ => new ComputedState<int>.Options() { Category = category },
            CreateInstanceOptions,
        ];
        foreach (var factory in factories) {
            var validate = () => ComputedStateComponent.ValidateStateOptionsFactory(typeof(ProbeComponent), factory);
            validate.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(ProbeComponent)}*");
        }
    }

    [Fact]
    public void GetStateOptionsShouldValidateOnlyOnCacheMiss()
    {
        var oldValidator = ComputedStateComponent.StateOptionsFactoryValidator;
        var validatedTypes = new List<Type>();
        try {
            ComputedStateComponent.StateOptionsFactoryValidator = (t, f) => {
                validatedTypes.Add(t);
                ComputedStateComponent.ValidateStateOptionsFactory(t, f);
            };

            var options1 = ComputedStateComponent.GetStateOptions<int>(typeof(CachedProbeComponent),
                static t => new ComputedState<int>.Options() { Category = t.Name });
            var options2 = ComputedStateComponent.GetStateOptions<int>(typeof(CachedProbeComponent),
                _ => new ComputedState<int>.Options() { Category = _category });
            options2.Should().BeSameAs(options1);
            options1.Category.Should().Be(nameof(CachedProbeComponent));
            validatedTypes.Should().Equal(typeof(CachedProbeComponent));

            var getOptions = () => ComputedStateComponent.GetStateOptions<int>(typeof(RejectedProbeComponent),
                _ => new ComputedState<int>.Options() { Category = _category });
            getOptions.Should().Throw<InvalidOperationException>();

            ComputedStateComponent.StateOptionsFactoryValidator = null;
            var options3 = getOptions.Invoke();
            options3.Category.Should().Be(_category);
        }
        finally {
            ComputedStateComponent.StateOptionsFactoryValidator = oldValidator;
        }
    }

    private static IComputedStateOptions CreateOptions(Type componentType)
        => new ComputedState<int>.Options() { Category = componentType.Name };

    private IComputedStateOptions CreateInstanceOptions(Type componentType)
        => new ComputedState<int>.Options() { Category = _category };

    // Nested types

    private sealed class ProbeComponent;
    private sealed class CachedProbeComponent;
    private sealed class RejectedProbeComponent;
}
