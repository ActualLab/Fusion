using ActualLab.Tests;

namespace ActualLab.Fusion.Tests;

public class FusionBuilderTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void NonSingletonComputeServiceWithMethodHandlerIsRejected(ServiceLifetime lifetime)
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddComputeService<MethodHandlerService>(lifetime);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be registered as a singleton*");
        fusion.Commander.Handlers
            .Should().NotContain(h => h.GetHandlerServiceType() == typeof(MethodHandlerService));
    }

    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void NonSingletonComputeServiceWithInterfaceHandlerIsRejected(ServiceLifetime lifetime)
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddComputeService<InterfaceHandlerService>(lifetime);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be registered as a singleton*");
        fusion.Commander.Handlers
            .Should().NotContain(h => h.GetHandlerServiceType() == typeof(InterfaceHandlerService));
    }

    [Fact]
    public void ScopedComputeServiceWithoutHandlersIsAccepted()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddComputeService<PlainService>(ServiceLifetime.Scoped);
        services.Should().Contain(d => d.ServiceType == typeof(PlainService) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void ScopedComputeServiceWithHandlersOptOutIsAccepted()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddComputeService<MethodHandlerService>(ServiceLifetime.Scoped, hasCommandHandlers: false);
        services.Should().Contain(d => d.ServiceType == typeof(MethodHandlerService) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void SingletonComputeServiceWithHandlersIsAccepted()
    {
        var fusion = new ServiceCollection().AddFusion();
        fusion.AddComputeService<MethodHandlerService>();
        fusion.Commander.Handlers
            .Should().Contain(h => h.GetHandlerServiceType() == typeof(MethodHandlerService));
    }

    [Theory]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void NonSingletonAddServiceWithHandlersIsRejected(ServiceLifetime lifetime)
    {
        var fusion = new ServiceCollection().AddFusion();
        var act = () => fusion.AddService<MethodHandlerService>(lifetime);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be registered as a singleton*");
    }

    [Fact]
    public void NonSingletonAddServiceWithHandlersOptOutIsAccepted()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddService<MethodHandlerService>(ServiceLifetime.Scoped, hasCommandHandlers: false);
        services.Should().Contain(d => d.ServiceType == typeof(MethodHandlerService) && d.Lifetime == ServiceLifetime.Scoped);
    }

    // Nested types

    public record FusionBuilderTestCommand : ICommand<Unit>;

    public class MethodHandlerService : IComputeService, IHasDisposeStatus
    {
        public bool IsDisposed => false;

        [CommandHandler]
        public virtual Task OnCommand(FusionBuilderTestCommand command, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public class InterfaceHandlerService : IComputeService, IHasDisposeStatus,
        ICommandHandler<FusionBuilderTestCommand>
    {
        public bool IsDisposed => false;

        public Task OnCommand(FusionBuilderTestCommand command, CommandContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    public class PlainService : IComputeService, IHasDisposeStatus
    {
        public bool IsDisposed => false;

        [ComputeMethod]
        public virtual Task<int> Get(int value)
            => Task.FromResult(value);
    }
}
