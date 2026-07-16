using ActualLab.Testing.Collections;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests;

public class ComputeMethodRegistryLookupTest(ITestOutputHelper @out) : TestBase(@out)
{
    public interface IService : IComputeService
    {
        [ComputeMethod]
        Task<long> Get(long key, CancellationToken cancellationToken);
    }

    public class Service : IService, IHasDisposeStatus
    {
        public ThreadSafe<int> CallCount { get; } = 0;
        public bool IsDisposed => false;

        public virtual Task<long> Get(long key, CancellationToken cancellationToken)
        {
            CallCount.Value++;
            return Task.FromResult(key);
        }
    }

    [Fact]
    public async Task LookupMustUseArgumentsExceptCancellationTokenAndProxyIdentity()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddService<IService, Service>(ServiceLifetime.Scoped, hasCommandHandlers: false);
        await using var serviceProvider = services.BuildServiceProvider();

        await using var scope1 = serviceProvider.CreateAsyncScope();
        var service1 = scope1.ServiceProvider.GetRequiredService<IService>();
        var service1Impl = (Service)service1;
        using var cancellationSource1 = new CancellationTokenSource();
        using var cancellationSource2 = new CancellationTokenSource();

        var computed1 = await Computed.Capture(() => service1.Get(1, cancellationSource1.Token));
        var computed2 = await Computed.Capture(() => service1.Get(1, cancellationSource2.Token));
        var computed3 = await Computed.Capture(() => service1.Get(2, cancellationSource1.Token));

        computed2.Should().BeSameAs(computed1);
        computed3.Should().NotBeSameAs(computed1);
        service1Impl.CallCount.Value.Should().Be(2);

        await using var scope2 = serviceProvider.CreateAsyncScope();
        var service2 = scope2.ServiceProvider.GetRequiredService<IService>();
        var computed4 = await Computed.Capture(() => service2.Get(1, cancellationSource1.Token));

        computed4.Should().NotBeSameAs(computed1);
        ((Service)service2).CallCount.Value.Should().Be(1);
    }
}
