using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Tests;

public class ComputedRegistryRegistrationTest
{
    [Fact]
    public async Task ReplacesExistingComputedSynchronously()
    {
        await using var services = CreateServices();
        var service = services.GetRequiredService<IRegistryComputeService>();
        var existing = await Computed.Capture(() => service.Get(1, default));
        var input = (ComputeMethodInput)existing.Input;

        var successor = new ComputeMethodComputed<int>(existing.Options, input);

        existing.ConsistencyState.Should().Be(ConsistencyState.Invalidated);
        ComputedImpl.TrySetValue(successor, 2).Should().BeTrue();
        ComputedRegistry.Get(input).Should().BeSameAs(successor);
    }

    [Fact]
    public async Task ConcurrentRegistrationLeavesOneConsistentComputed()
    {
        await using var services = CreateServices();
        var service = services.GetRequiredService<IRegistryComputeService>();
        var existing = await Computed.Capture(() => service.Get(1, default));
        var input = (ComputeMethodInput)existing.Input;
        var successors = new ConcurrentBag<ComputeMethodComputed<int>>();

        await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() => {
            var successor = new ComputeMethodComputed<int>(existing.Options, input);
            successors.Add(successor);
            ComputedImpl.TrySetValue(successor, i);
        })));

        var registered = ComputedRegistry.Get(input);
        registered.Should().NotBeNull();
        successors.Should().Contain((ComputeMethodComputed<int>)registered!);
        registered!.ConsistencyState.Should().Be(ConsistencyState.Consistent);
        successors.Count(c => c.ConsistencyState == ConsistencyState.Consistent).Should().Be(1);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddFusion().AddComputeService<IRegistryComputeService, RegistryComputeService>();
        return services.BuildServiceProvider();
    }

    public interface IRegistryComputeService : IComputeService
    {
        [ComputeMethod]
        Task<int> Get(long key, CancellationToken cancellationToken);
    }

    public class RegistryComputeService : IRegistryComputeService
    {
        public virtual Task<int> Get(long key, CancellationToken cancellationToken)
            => Task.FromResult(1);
    }
}
