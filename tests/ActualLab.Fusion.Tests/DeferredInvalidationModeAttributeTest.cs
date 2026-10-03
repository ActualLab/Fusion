using ActualLab.Reflection;

namespace ActualLab.Fusion.Tests;

// How a handler's mode is found. These are regressions: a handler registered under an interface has
// an interface MethodInfo, so anything declared on the implementation is only reachable on purpose.
public class DeferredInvalidationModeAttributeTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    [Fact]
    public void AMethodAttributeOnTheImplementationIsFoundBehindAnInterface()
    {
        // Regression: handlers of a Server/Distributed service are registered under the interface,
        // so Method is IService.OnSet. Attribute inheritance runs from an override toward what it
        // overrides, never the other way, so this has to be looked up through the interface map -
        // without it the method's Distributed was silently ignored in favour of the class's Local.
        var method = typeof(IService).GetMethod(nameof(IService.OnSet))!;

        var attribute = DeferredInvalidationModeAttribute.Get(method, typeof(Service));

        attribute!.Mode.Should().Be(DeferredInvalidationMode.Distributed);
    }

    [Fact]
    public void TheClassAttributeAppliesToAMethodThatDeclaresNone()
    {
        var method = typeof(IService).GetMethod(nameof(IService.OnOther))!;

        DeferredInvalidationModeAttribute.Get(method, typeof(Service))!
            .Mode.Should().Be(DeferredInvalidationMode.Local);
    }

    [Fact]
    public void AnInterfaceMethodAttributeStillWins()
    {
        // The method's own attribute is checked before the implementation is consulted at all
        var method = typeof(IDeclaredOnInterface).GetMethod(nameof(IDeclaredOnInterface.OnSet))!;

        DeferredInvalidationModeAttribute.Get(method, typeof(DeclaredOnInterface))!
            .Mode.Should().Be(DeferredInvalidationMode.Replicated);
    }

    [Fact]
    public void TheMostDerivedInterfaceWinsAndTheChoiceIsStable()
    {
        // Regression: this used to walk Type.GetInterfaces(), whose order isn't specified, so a
        // service implementing two mode-carrying interfaces resolved to an arbitrary one
        var method = typeof(IBase).GetMethod(nameof(IBase.OnSet))!;

        var attribute = DeferredInvalidationModeAttribute.Get(method, typeof(TwoInterfaces));

        // IDerived extends IBase, so it's the more derived declaration and the one that applies
        attribute!.Mode.Should().Be(DeferredInvalidationMode.Distributed);
    }

    [Fact]
    public void InterfacesByDependencyPutsADerivedInterfaceFirst()
    {
        var interfaces = typeof(TwoInterfaces).GetInterfacesByDependency();

        interfaces.Should().Contain(typeof(IBase)).And.Contain(typeof(IDerived));
        Array.IndexOf(interfaces, typeof(IDerived))
            .Should().BeLessThan(Array.IndexOf(interfaces, typeof(IBase)));
        // Same answer every time - the point of the ordering
        typeof(TwoInterfaces).GetInterfacesByDependency().Should().Equal(interfaces);
    }

    [Fact]
    public void NoAttributeAnywhereResolvesToNull()
    {
        var method = typeof(IUndeclared).GetMethod(nameof(IUndeclared.OnSet))!;

        DeferredInvalidationModeAttribute.Get(method, typeof(Undeclared)).Should().BeNull();
    }

    // Nested types

    public interface IService
    {
        Task OnSet(CancellationToken cancellationToken = default);
        Task OnOther(CancellationToken cancellationToken = default);
    }

    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public class Service : IService
    {
        [DeferredInvalidationMode(DeferredInvalidationMode.Distributed)]
        public virtual Task OnSet(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public virtual Task OnOther(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public interface IDeclaredOnInterface
    {
        [DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
        Task OnSet(CancellationToken cancellationToken = default);
    }

    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public class DeclaredOnInterface : IDeclaredOnInterface
    {
        public virtual Task OnSet(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
    public interface IBase
    {
        Task OnSet(CancellationToken cancellationToken = default);
    }

    [DeferredInvalidationMode(DeferredInvalidationMode.Distributed)]
    public interface IDerived : IBase;

    public class TwoInterfaces : IDerived
    {
        public virtual Task OnSet(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public interface IUndeclared
    {
        Task OnSet(CancellationToken cancellationToken = default);
    }

    public class Undeclared : IUndeclared
    {
        public virtual Task OnSet(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
