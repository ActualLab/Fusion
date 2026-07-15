namespace ActualLab.Fusion.Tests.Audit;

public class FusionStateContractAuditTest
{
    [Fact]
    public void UntypedLastNonErrorValueReturnsTheValue()
    {
        var state = StateFactory.Default.NewMutable(42);

        ((IState)state).LastNonErrorValue.Should().Be(42);
    }

    [Fact]
    public async Task UpdateDelayerWithoutTrackerPropagatesCancellation()
    {
        var delayer = new UpdateDelayer(null, TimeSpan.FromSeconds(1), FixedDelayer.Defaults.RetryDelays);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => delayer.Delay(0, cancellationSource.Token));
    }

    [Fact]
    public void InvalidationSourceEnumerationStopsAtTheEndOfTheChain()
    {
        var source = new InvalidationSource("origin");

        source.Take(2).Count().Should().Be(1);
    }

    [Fact]
    public async Task SynchronizePropagatesCancellation()
    {
        var computed = Computed.New(_ => Task.FromResult(1));
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var synchronizer = new CancelingSynchronizer();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => synchronizer.Synchronize(computed, cancellationSource.Token).AsTask());
    }

    [Fact]
    public async Task InvalidatingInvokesEverySubscriberWhenOneThrows()
    {
        var computed = await Computed.New(_ => Task.FromResult(1)).Update();
        var callCount = 0;
        computed.Invalidated += _ => throw new InvalidOperationException("failure");
        computed.Invalidated += _ => callCount++;

        computed.Invalidate(immediately: true);

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ComputedSourceUpdateSurvivesThrowingUpdatedSubscriber()
    {
        var source = new ComputedSource<int>(
            Computed.DefaultServices,
            (_, _) => Task.FromResult(1));
        source.Updated += _ => throw new InvalidOperationException("failure");

        await source.Update();

        source.Computed.Value.Should().Be(1);
    }

    [Fact]
    public void RemoteCacheBuilderCompletesWiringForPreRegisteredImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Client.Caching.InMemoryRemoteComputedCache>();

        services.AddFusion().AddInMemoryRemoteComputedCache();

        services.Should().Contain(x => x.ServiceType == typeof(Client.Caching.IRemoteComputedCache));
    }

    [Fact]
    public void OperationReprocessorBuilderCompletesWiringForPreRegisteredImplementation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Operations.Reprocessing.OperationReprocessor>();

        services.AddFusion().AddOperationReprocessor();

        services.Should().Contain(x => x.ServiceType == typeof(Operations.Reprocessing.IOperationReprocessor));
    }

    private sealed class CancelingSynchronizer : ComputedSynchronizer
    {
        public override bool IsSynchronized(Client.IRemoteComputed computed)
            => false;

        public override Task WhenSynchronized(Client.IRemoteComputed computed, CancellationToken cancellationToken)
            => Task.FromCanceled(cancellationToken);

        public override Task WhenSynchronized(Computed computed, CancellationToken cancellationToken)
            => Task.FromCanceled(cancellationToken);
    }
}
