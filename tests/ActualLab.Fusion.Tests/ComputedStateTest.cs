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

    [Fact]
    public void NonTransientErrorInvalidationDelayDefaultsTest()
    {
        ComputedOptions.Default.NonTransientErrorInvalidationDelay.Should().Be(TimeSpan.FromSeconds(30));
        ComputedOptions.ClientDefault.NonTransientErrorInvalidationDelay.Should().Be(TimeSpan.FromSeconds(30));
        ComputedOptions.MutableStateDefault.NonTransientErrorInvalidationDelay.Should().Be(TimeSpan.MaxValue);
    }

    [Fact]
    public async Task NonTransientErrorHorizonAutoInvalidatesTest()
    {
        var services = CreateServices(services => {
            services.AddTransiencyResolver<Computed>(_ => e =>
                (e is InvalidOperationException ? Transiency.NonTransient : Transiency.Unknown)
                    .Or(e, TransiencyResolvers.PreferNonTransient));
        });
        var stateFactory = services.StateFactory();

        var count = 0;
        var horizon = TimeSpan.FromSeconds(0.5);
        var state = stateFactory.NewComputed(
            new ComputedState<int>.Options() {
                UpdateDelayer = FixedDelayer.NextTick,
                InitialValue = -1,
                ComputedOptions = ComputedOptions.Default with {
                    NonTransientErrorInvalidationDelay = horizon,
                },
            },
            _ => {
                Interlocked.Increment(ref count);
                throw new InvalidOperationException("boom");
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => state.Use());
        var c = state.Computed;
        c.HasError.Should().BeTrue();
        c.IsConsistent().Should().BeTrue();

        // A non-transient error must auto-invalidate once the configured error horizon elapses
        await c.WhenInvalidated().WaitAsync(horizon + TimeSpan.FromSeconds(3));
        c.IsInvalidated().Should().BeTrue();
    }

    [Fact]
    public async Task ThrowingTransiencyResolverIsTreatedAsTransientTest()
    {
        var services = CreateServices(services => {
            services.AddTransiencyResolver<Computed>(_ => _ => throw new InvalidOperationException("resolver is broken"));
        });
        var stateFactory = services.StateFactory();

        var count = 0;
        var delay = TimeSpan.FromSeconds(0.5);
        var state = stateFactory.NewComputed(
            new ComputedState<int>.Options() {
                UpdateDelayer = FixedDelayer.NextTick,
                InitialValue = -1,
                ComputedOptions = ComputedOptions.Default with {
                    TransientErrorInvalidationDelay = delay,
                    NonTransientErrorInvalidationDelay = TimeSpan.MaxValue,
                },
            },
            _ => {
                Interlocked.Increment(ref count);
                throw new InvalidOperationException("boom");
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => state.Use());
        var c = state.Computed;
        c.HasError.Should().BeTrue();
        c.IsConsistent().Should().BeTrue();

        // A throwing TransiencyResolver is classified as transient, so the error auto-invalidates
        // via TransientErrorInvalidationDelay instead of being cached until the horizon (here disabled).
        await c.WhenInvalidated().WaitAsync(delay + TimeSpan.FromSeconds(3));
        c.IsInvalidated().Should().BeTrue();
    }

    [Fact]
    public async Task InvalidatedEventFiresForBornInvalidatedGenerationTest()
    {
        // A dependency that invalidates the state's computed while it is still computing makes
        // the published generation "born invalidated". The per-generation Invalidated event must
        // still fire for it (previously it was skipped because the snapshot didn't yet point to
        // the just-invalidated computed when its invalidation ran).
        var services = CreateServices();
        var stateFactory = services.StateFactory();
        var dep = stateFactory.NewMutable(0);

        var computeGate = TaskCompletionSourceExt.New<Unit>();
        var computeCount = 0;
        var state = stateFactory.NewComputed(
            new ComputedState<int>.Options() {
                UpdateDelayer = FixedDelayer.NextTick,
                InitialValue = -1,
            },
            async ct => {
                var value = await dep.Use(ct).ConfigureAwait(false); // Establishes the dependency edge
                var index = Interlocked.Increment(ref computeCount);
                WriteLine($"Compute #{index}: read {value}, waiting on gate...");
                await computeGate.Task.ConfigureAwait(false);
                return value;
            });

        var invalidatedCount = 0;
        state.Invalidated += (_, _) => Interlocked.Increment(ref invalidatedCount);

        // Wait for the first real computation to reach the gate
        await Task.Delay(300);
        computeCount.Should().Be(1);

        // Invalidate the dependency while the state is computing -> the in-flight computed is
        // flagged for invalidation and is published already invalidated.
        dep.Value = 1;
        await Task.Delay(100);

        // Let the born-invalidated generation complete & get published
        computeGate.SetResult(default);
        await Task.Delay(300);

        invalidatedCount.Should().BeGreaterThanOrEqualTo(1,
            "the Invalidated event must fire for a generation that was invalidated while computing");
    }

    [Fact]
    public async Task RecomputeDuringComputationTest()
    {
        // This test reproduces the MixedStateComponent bug:
        // if Recompute() is called while a computation is in progress,
        // it targets the old (already invalidated) computed via state.Computed,
        // making it a no-op. The result is stale state.

        var services = CreateServices();
        var stateFactory = services.StateFactory();

        // Simulate MutableState that MixedStateComponent uses
        var mutableState = stateFactory.NewMutable("v0");

        // A gate to pause computation mid-flight
        var computeGate = TaskCompletionSourceExt.New<Unit>();
        var computeCount = 0;

        // Create a ComputedState that reads MutableState.Value directly (not via Use()),
        // just like MixedStateComponent.ComputeState typically does
        var state = stateFactory.NewComputed(
            new ComputedState<string>.Options() {
                UpdateDelayer = FixedDelayer.NextTick,
                InitialValue = "",
            },
            async _ => {
                var value = mutableState.Value;
                var index = Interlocked.Increment(ref computeCount);
                WriteLine($"Compute #{index}: reading '{value}', waiting on gate...");
                await computeGate.Task.ConfigureAwait(false);
                WriteLine($"Compute #{index}: gate opened, returning '{value}'");
                return value;
            });

        // Wire up the MixedStateComponent pattern:
        // MutableState.Updated triggers State.Recompute()
        mutableState.Updated += (_, _) => _ = state.Recompute();

        // Let initial computation (#1, reading "v0") start and complete
        await Task.Delay(100);
        computeCount.Should().Be(1);
        computeGate.SetResult(default);
        await Task.Delay(200);
        state.Value.Should().Be("v0");

        // Step 1: Set v1 — this triggers Recompute(), starts computation #2.
        // Use a new gate to pause #2 mid-computation.
        computeGate = TaskCompletionSourceExt.New<Unit>();
        mutableState.Value = "v1";
        await Task.Delay(200);
        computeCount.Should().Be(2); // Computation #2 started, paused at gate

        // Step 2: While computation #2 is paused (reading "v1"),
        // update MutableState to "v2". This triggers Recompute() again,
        // but state.Computed is the OLD invalidated computed — so Invalidate is a no-op.
        mutableState.Value = "v2";
        await Task.Delay(100);

        // Step 3: Release the gate — computation #2 finishes with "v1"
        computeGate.SetResult(default);
        await Task.Delay(500);

        // BUG (before fix): state shows "v1" even though MutableState is "v2"
        var stateValue = state.Value;
        var mutableValue = mutableState.Value;
        WriteLine($"State.Value = '{stateValue}', MutableState.Value = '{mutableValue}'");
        stateValue.Should().Be(mutableValue,
            "State must reflect the latest MutableState value, but Recompute() during computation was a no-op");
    }
}
