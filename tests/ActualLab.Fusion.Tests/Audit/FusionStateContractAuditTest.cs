using System.Reflection;
using ActualLab.Fusion.Internal;

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
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => synchronizer.Synchronize(Task.FromResult<Computed>(computed), cancellationSource.Token));
    }

    [Fact]
    public async Task SynchronizePropagatesFaults()
    {
        var computed = Computed.New(_ => Task.FromResult(1));
        var synchronizer = new FaultingSynchronizer();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => synchronizer.Synchronize(computed, CancellationToken.None).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => synchronizer.Synchronize(Task.FromResult<Computed>(computed), CancellationToken.None));
    }

    [Fact]
    public async Task InvalidatingInvokesEverySubscriberWhenOneThrows()
    {
        var computed = await Computed.New(_ => Task.FromResult(1)).Update();
        var calls = new List<int>();
        computed.Invalidated += _ => calls.Add(1);
        computed.Invalidated += _ => {
            calls.Add(2);
            throw new InvalidOperationException("failure");
        };
        computed.Invalidated += _ => calls.Add(3);

        computed.Invalidate(immediately: true);

        calls.Should().Equal(1, 2, 3);
        var handlerSet = typeof(Computed)
            .GetField("_invalidated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(computed)!;
        handlerSet.GetType()
            .GetField("_storage", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(handlerSet)
            .Should().BeNull();
    }

    [Fact]
    public async Task ComputedSourceUpdateSurvivesThrowingUpdatedSubscriber()
    {
        var source = new ComputedSource<int>(
            Computed.DefaultServices,
            (_, _) => Task.FromResult(1));
        var sourceLock = typeof(ComputedSource)
            .GetProperty("Lock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(source)!;
        var wasLockHeld = false;
        var laterSubscriberCallCount = 0;
        Action<Computed> throwingSubscriber = _ => {
            wasLockHeld = Monitor.IsEntered(sourceLock);
            throw new InvalidOperationException("failure");
        };
        Action<Computed> laterSubscriber = _ => laterSubscriberCallCount++;
        source.Updated += throwingSubscriber + laterSubscriber;

        await source.Update();

        wasLockHeld.Should().BeFalse();
        laterSubscriberCallCount.Should().Be(1);
        source.Computed.ConsistencyState.Should().Be(ConsistencyState.Consistent);
        source.Computed.Value.Should().Be(1);

        source.Updated -= throwingSubscriber;
        source.Computed.Invalidate(immediately: true);
        await source.Update();
        laterSubscriberCallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetDependantsUsesOneAtomicSnapshot()
    {
        var dependency = await Computed.New(_ => Task.FromResult(1)).Update();
        var dependantSource = new ComputedSource<int>(
            Computed.DefaultServices,
            (_, _) => Task.FromResult(2));
        var dependant = new ComputedSourceComputed<int>(ComputedOptions.Default, dependantSource);
        const int readerCount = 8;
        using var readersStarted = new CountdownEvent(readerCount);
        var readers = new Task<(ComputedInput Input, ulong Version)[]>[readerCount];

        lock (dependency) {
            for (var i = 0; i < readers.Length; i++)
                readers[i] = Task.Run(() => {
                    readersStarted.Signal();
                    return ComputedImpl.GetDependants(dependency);
                });
            readersStarted.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
            Thread.Sleep(50);
            ComputedImpl.AddDependency(dependant, dependency);
            ComputedImpl.GetDependants(dependency).Should().ContainSingle();
        }

        var snapshots = await Task.WhenAll(readers);
        snapshots.Should().OnlyContain(x => x.Length == 1);
    }

    [Fact]
    public async Task ConcurrentDependencyInvalidationCleansEveryGraphEdge()
    {
        for (var i = 0; i < 100; i++) {
            var left = await Computed.New(_ => Task.FromResult(1)).Update();
            var right = await Computed.New(_ => Task.FromResult(2)).Update();
            var dependant = await Computed.New(async ct => {
                var leftValue = await left.Use(ct);
                var rightValue = await right.Use(ct);
                return leftValue + rightValue;
            }).Update();

            ComputedImpl.GetDependants(left).Should().ContainSingle();
            ComputedImpl.GetDependants(right).Should().ContainSingle();
            ComputedImpl.GetDependencies(dependant).Should().HaveCount(2);

            await Task.WhenAll(
                Task.Run(() => left.Invalidate(immediately: true)),
                Task.Run(() => right.Invalidate(immediately: true)));

            left.ConsistencyState.Should().Be(ConsistencyState.Invalidated);
            right.ConsistencyState.Should().Be(ConsistencyState.Invalidated);
            dependant.ConsistencyState.Should().Be(ConsistencyState.Invalidated);
            ComputedImpl.GetDependants(left).Should().BeEmpty();
            ComputedImpl.GetDependants(right).Should().BeEmpty();
            ComputedImpl.GetDependencies(dependant).Should().BeEmpty();
        }
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

    private sealed class FaultingSynchronizer : ComputedSynchronizer
    {
        public override bool IsSynchronized(Client.IRemoteComputed computed)
            => false;

        public override Task WhenSynchronized(Client.IRemoteComputed computed, CancellationToken cancellationToken)
            => Task.FromException(new InvalidOperationException("failure"));

        public override Task WhenSynchronized(Computed computed, CancellationToken cancellationToken)
            => Task.FromException(new InvalidOperationException("failure"));
    }
}
