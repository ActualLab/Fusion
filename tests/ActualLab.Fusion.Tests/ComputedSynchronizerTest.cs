using ActualLab.Fusion.Client;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Tests.Services;

namespace ActualLab.Fusion.Tests;

public class ComputedSynchronizerTest : FusionTestBase
{
    public ComputedSynchronizerTest(ITestOutputHelper @out) : base(@out)
        => UseRemoteComputedCache = true;

    protected override void ConfigureTestServices(IServiceCollection services, bool isClient)
    {
        base.ConfigureTestServices(services, isClient);
        var fusion = services.AddFusion();
        if (!isClient)
            fusion.AddService<IKeyValueService<string>, KeyValueService<string>>();
        else
            fusion.AddClient<IKeyValueService<string>>();
    }

    // ==========================================================================
    // ComputedSynchronizer.None tests
    // ==========================================================================

    [Fact]
    public async Task None_IsSynchronized_AlwaysTrue()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        var c1 = await GetComputed(kv1, "1");

        // ComputedSynchronizer.None always returns true
        var none = ComputedSynchronizer.None.Instance;
        c1.IsSynchronized(none).Should().BeTrue();
    }

    [Fact]
    public async Task None_WhenSynchronized_CompletesImmediately()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        var c1 = await GetComputed(kv1, "1");

        // ComputedSynchronizer.None always completes immediately
        var none = ComputedSynchronizer.None.Instance;
        var task = c1.WhenSynchronized(none);
        task.IsCompleted.Should().BeTrue();
    }

    // ==========================================================================
    // ComputedSynchronizer.Precise tests
    // ==========================================================================

    [Fact]
    public async Task Precise_IsSynchronized_WhenCompleted()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");

        // First client: nothing cached, RPC call happens
        var c1 = await GetComputed(kv1, "1");
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        var precise = ComputedSynchronizer.Precise.Instance;
        c1.IsSynchronized(precise).Should().BeTrue();

        // Second client: cached, not yet synchronized
        var c2 = await GetComputed(kv2, "1");
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();
        c2.IsSynchronized(precise).Should().BeFalse();

        // Wait for synchronization
        await c2.WhenSynchronized;
        c2.IsSynchronized(precise).Should().BeTrue();
    }

    [Fact]
    public async Task Precise_WhenSynchronized_WaitsForCompletion()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");

        // First client populates cache
        var c1 = await GetComputed(kv1, "1");
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        // Second client: cached value, needs synchronization
        var precise = ComputedSynchronizer.Precise.Instance;
        var c2 = await GetComputed(kv2, "1");

        var whenSyncTask = c2.WhenSynchronized(precise);
        whenSyncTask.IsCompleted.Should().BeFalse();

        await whenSyncTask;
        c2.WhenSynchronized.IsCompleted.Should().BeTrue();
    }

    // ==========================================================================
    // ComputedSynchronizer.Safe tests
    // ==========================================================================

    [Fact]
    public async Task Safe_WhenSynchronized_RespectsMaxDuration()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");

        // First client populates cache
        var c1 = await GetComputed(kv1, "1");

        // Set long delay on server
        ((KeyValueService<string>)kv).GetMethodDelay = TimeSpan.FromSeconds(10);

        // Create synchronizer with short timeout
        var safe = new ComputedSynchronizer.Safe {
            AssumeSynchronizedWhenDisconnected = false,
            AssumeSynchronizedWhenRemoteComputedCacheHasHitToCallDelayer = false,
            MaxSynchronizeDurationProvider = _ => TimeSpan.FromMilliseconds(100),
        };

        // Second client: should timeout
        var c2 = await GetComputed(kv2, "1");

        var sw = Stopwatch.StartNew();
        using (safe.Activate()) {
            await c2.WhenSynchronized();
        }
        sw.Stop();

        // Should complete within reasonable time due to timeout
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Safe_IsSynchronized_WhenAlreadySynced()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");

        var c1 = await GetComputed(kv1, "1");
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();

        var safe = ComputedSynchronizer.Safe.Instance;
        c1.IsSynchronized(safe).Should().BeTrue();
    }

    [Fact]
    public async Task Safe_IsSynchronized_WhenAssumeSynchronized()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");

        // Populate cache
        var c1 = await GetComputed(kv1, "1");

        // Make sync slow
        ((KeyValueService<string>)kv).GetMethodDelay = TimeSpan.FromSeconds(10);

        var safe = new ComputedSynchronizer.Safe {
            AssumeSynchronizedWhenDisconnected = false,
            AssumeSynchronizedWhenRemoteComputedCacheHasHitToCallDelayer = false,
            AssumeSynchronized = true,
        };

        var c2 = await GetComputed(kv2, "1");
        c2.WhenSynchronized.IsCompleted.Should().BeFalse();

        // With AssumeSynchronized = true, should return true even if not actually synced
        c2.IsSynchronized(safe).Should().BeTrue();
    }

    // ==========================================================================
    // ComputedSynchronizerScope tests
    // ==========================================================================

    [Fact]
    public void Scope_ActivatesAndDeactivates()
    {
        var originalCurrent = ComputedSynchronizer.Current;
        var precise = ComputedSynchronizer.Precise.Instance;

        using (precise.Activate()) {
            ComputedSynchronizer.Current.Should().BeSameAs(precise);
        }

        ComputedSynchronizer.Current.Should().BeSameAs(originalCurrent);
    }

    [Fact]
    public void Scope_NestedActivation()
    {
        var originalCurrent = ComputedSynchronizer.Current;
        var precise = ComputedSynchronizer.Precise.Instance;
        var none = ComputedSynchronizer.None.Instance;

        using (precise.Activate()) {
            ComputedSynchronizer.Current.Should().BeSameAs(precise);

            using (none.Activate()) {
                ComputedSynchronizer.Current.Should().BeSameAs(none);
            }

            ComputedSynchronizer.Current.Should().BeSameAs(precise);
        }

        ComputedSynchronizer.Current.Should().BeSameAs(originalCurrent);
    }

    // ==========================================================================
    // State synchronization tests
    // ==========================================================================

    [Fact]
    public async Task State_MutableState_AlwaysSynchronized()
    {
        var services = CreateServices(true);
        await using var _ = services as IAsyncDisposable;

        var mutableState = services.StateFactory().NewMutable<int>(10);
        var computed = mutableState.Computed;

        var precise = ComputedSynchronizer.Precise.Instance;
        computed.IsSynchronized(precise).Should().BeTrue();

        var whenSync = precise.WhenSynchronized(computed, CancellationToken.None);
        whenSync.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task State_ComputedState_InitialState_NotSynchronized()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        // Populate cache
        await kv1.Get("1");

        // Create state that hasn't computed yet
        var tcs = new TaskCompletionSource<Unit>();
        var state = clientServices2.StateFactory().NewComputed<string>(
            new ComputedState<string>.Options {
                TryComputeSynchronously = false,
            },
            async ct => {
                await tcs.Task;
                return await kv2.Get("1", ct);
            });

        var snapshot = state.Snapshot;
        snapshot.IsInitial.Should().BeTrue();

        var computed = state.Computed;

        // Initial state should not be synchronized
        var precise = ComputedSynchronizer.Precise.Instance;
        computed.IsSynchronized(precise).Should().BeFalse();

        // Release the state computation
        tcs.SetResult(default);
        await state.WhenNonInitial();

        // Now should be synchronized (after RPC completes)
        await state.Computed.WhenSynchronized(precise);
        state.Computed.IsSynchronized(precise).Should().BeTrue();
    }

    [Fact]
    public async Task State_ComputedState_WithDependencies_ChecksDependencies()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        await kv.Set("2", "b");

        // First client populates cache
        await kv1.Get("1");
        await kv1.Get("2");

        // Create state that depends on two remote computed values
        var state = clientServices2.StateFactory().NewComputed<string>(
            FixedDelayer.Get(0.1),
            async (_, ct) => {
                var v1 = await kv2.Get("1", ct);
                var v2 = await kv2.Get("2", ct);
                return $"{v1} {v2}";
            });

        await state.WhenNonInitial();
        state.Value.Should().Be("a b");

        // Wait for all to synchronize
        await state.Computed.WhenSynchronized(ComputedSynchronizer.Precise.Instance);
        state.Computed.IsSynchronized(ComputedSynchronizer.Precise.Instance).Should().BeTrue();
    }

    // ==========================================================================
    // Synchronize method tests
    // ==========================================================================

    [Fact]
    public async Task Synchronize_UpdatesComputedIfInvalidated()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        var c1 = await GetComputed(kv1, "1");
        c1.Value.Should().Be("a");

        // Invalidate and change value
        await kv.Set("1", "b");
        await c1.WhenInvalidated().WaitAsync(TimeSpan.FromSeconds(1));
        c1.IsConsistent().Should().BeFalse();

        // Synchronize should update the computed
        var precise = ComputedSynchronizer.Precise.Instance;
        var synchronized = await precise.Synchronize(c1, CancellationToken.None);
        synchronized.Value.Should().Be("b");
        synchronized.IsConsistent().Should().BeTrue();
    }

    [Fact]
    public async Task Synchronize_GenericVersion_Works()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        var c1 = await Computed.Capture(() => kv1.Get("1"));
        c1.Value.Should().Be("a");

        // Generic Synchronize
        var precise = ComputedSynchronizer.Precise.Instance;
        var synchronized = await c1.Synchronize(precise, CancellationToken.None);
        synchronized.Value.Should().Be("a");
        synchronized.Should().BeOfType<RemoteComputed<string>>();
    }

    // ==========================================================================
    // Cancellation tests
    // ==========================================================================

    [Fact]
    public async Task WhenSynchronized_RespectsCancellation_PreCancelled()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        await kv1.Get("1"); // Populate cache

        // Set delay BEFORE second client makes the call
        ((KeyValueService<string>)kv).GetMethodDelay = TimeSpan.FromSeconds(30);

        var c2 = await GetComputed(kv2, "1");

        // Test with pre-cancelled token - if not synchronized, should throw immediately
        if (!c2.WhenSynchronized.IsCompleted) {
            var precise = ComputedSynchronizer.Precise.Instance;
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => c2.WhenSynchronized(precise, cts.Token));
        }
    }

    [Fact]
    public async Task WhenSynchronized_CompletesNormally_WhenAlreadySynchronized()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        var c1 = await GetComputed(kv1, "1");

        // When already synchronized, WhenSynchronized should complete immediately
        // even with a very short timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
        var precise = ComputedSynchronizer.Precise.Instance;

        // Should not throw since it's already synchronized
        await c1.WhenSynchronized(precise, cts.Token);
        c1.WhenSynchronized.IsCompleted.Should().BeTrue();
    }

    // ==========================================================================
    // Extension method tests
    // ==========================================================================

    [Fact]
    public async Task ExtensionMethods_UseCurrentSynchronizer()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        var c1 = await GetComputed(kv1, "1");

        // Default is None, so IsSynchronized() should be true
        ComputedSynchronizer.Current.Should().Be(ComputedSynchronizer.None.Instance);
        c1.IsSynchronized().Should().BeTrue();

        // Using a scope should change behavior
        using (ComputedSynchronizer.Precise.Instance.Activate()) {
            c1.IsSynchronized().Should().BeTrue(); // Already synced
        }
    }

    [Fact]
    public async Task IsSynchronized_MatchesWhenSynchronized_Behavior()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        await kv1.Get("1"); // Populate cache

        var c2 = await GetComputed(kv2, "1");
        var precise = ComputedSynchronizer.Precise.Instance;

        // IsSynchronized should match WhenSynchronized.IsCompleted
        var isSynced = c2.IsSynchronized(precise);
        var whenSyncCompleted = c2.WhenSynchronized(precise).IsCompleted;

        isSynced.Should().Be(whenSyncCompleted);
    }

    // ==========================================================================
    // Dependencies synchronization tests
    // ==========================================================================

    [Fact]
    public async Task Dependencies_AllMustBeSynchronized()
    {
        await using var serving = await WebHost.Serve();
        await Delay(0.25);
        var cache = ClientServices.GetRequiredService<IRemoteComputedCache>();
        await cache.WhenInitialized;

        var clientServices2 = CreateServices(true);
        await using var _ = clientServices2 as IAsyncDisposable;

        var kv = WebServices.GetRequiredService<IKeyValueService<string>>();
        var kv1 = ClientServices.GetRequiredService<IKeyValueService<string>>();
        var kv2 = clientServices2.GetRequiredService<IKeyValueService<string>>();

        await kv.Set("1", "a");
        await kv.Set("2", "b");

        // Only populate cache for "1"
        await kv1.Get("1");

        // Create state depending on both
        var state = clientServices2.StateFactory().NewComputed<string>(
            FixedDelayer.Get(0.1),
            async (_, ct) => {
                var v1 = await kv2.Get("1", ct);
                var v2 = await kv2.Get("2", ct);
                return $"{v1} {v2}";
            });

        await state.WhenNonInitial();
        state.Value.Should().Be("a b");

        // Even though "1" was cached, "2" wasn't, so both should complete quickly
        // (RPC calls happen for both)
        await state.Computed.WhenSynchronized(ComputedSynchronizer.Precise.Instance);
        state.Computed.IsSynchronized(ComputedSynchronizer.Precise.Instance).Should().BeTrue();
    }

    // ==========================================================================
    // Helper methods
    // ==========================================================================

    private static async Task<RemoteComputed<string>> GetComputed(IKeyValueService<string> kv, string key)
    {
        var c = await Computed.Capture(() => kv.Get(key));
        return (RemoteComputed<string>)c;
    }
}
