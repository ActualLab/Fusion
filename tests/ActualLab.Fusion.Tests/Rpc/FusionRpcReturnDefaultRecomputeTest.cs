using ActualLab.Fusion.Client;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Fusion.Tests.Services;
using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Fusion.Tests.Rpc;

// A dependant's recompute must never observe the ReturnDefault default once the real value was seen,
// even when the invalidated remote computed it depends on got collected before the recompute reached it.
[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class FusionRpcReturnDefaultRecomputeTest(ITestOutputHelper @out) : SimpleFusionTestBase(@out)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    protected override void ConfigureServices(ServiceCollection services)
    {
        base.ConfigureServices(services);
        var fusion = services.AddFusion();
        fusion.AddServerAndClient<IReturnDefaultTester, ReturnDefaultTester>();
        fusion.AddService<ReturnDefaultConsumer>();
        services.AddSingleton<IRemoteComputedCache>(c
            => new InMemoryRemoteComputedCache(InMemoryRemoteComputedCache.Options.Default, c));
    }

    // keepAlive: false disables ComputedRegistry.Settings
    // .PostInvalidateKeepAliveDurationForRemoteComputedWithCacheOrReturnDefaultCacheMode,
    // which brings the bug back - the default (ReturnDefault) or a stale cached value (Cache) is yielded.
    [Theory]
    [InlineData(false, false, false, true, "v-1, b")]
    [InlineData(true, false, false, true, "v-1, b")]
    [InlineData(true, false, true, true, "v-1, b")]
    [InlineData(true, true, false, false, "v-1, b")]
    [InlineData(true, false, false, false, "v-1, <null>, b")]
    [InlineData(true, false, true, false, "v-1, v-1, b")]
    public async Task RecomputeAfterGCTest(
        bool mustCollect, bool mustHoldRemoteComputed, bool useCacheMode, bool keepAlive, string expected)
    {
        var oldKeepAliveDuration = ComputedRegistry.Settings.PostInvalidateKeepAliveDurationForRemoteComputedWithCacheOrReturnDefaultCacheMode;
        if (!keepAlive)
            ComputedRegistry.Settings.PostInvalidateKeepAliveDurationForRemoteComputedWithCacheOrReturnDefaultCacheMode = TimeSpan.Zero;
        try {
            var sequence = await GetSequence(mustCollect, mustHoldRemoteComputed, useCacheMode);
            var tail = sequence.SkipWhile(x => x != "v-1").Select(x => x ?? "<null>").ToDelimitedString(", ");
            tail.Should().Be(expected);
        }
        finally {
            ComputedRegistry.Settings.PostInvalidateKeepAliveDurationForRemoteComputedWithCacheOrReturnDefaultCacheMode = oldKeepAliveDuration;
        }
    }

    [Fact]
    public async Task KeepAliveExpiresTest()
    {
        var oldKeepAliveDuration = ComputedRegistry.Settings.PostInvalidateKeepAliveDurationForRemoteComputedWithCacheOrReturnDefaultCacheMode;
        ComputedRegistry.Settings.PostInvalidateKeepAliveDurationForRemoteComputedWithCacheOrReturnDefaultCacheMode = TimeSpan.FromSeconds(1);
        try {
            await using var services = CreateServices();
            var client = services.RpcHub().GetClient<IReturnDefaultTester>();
            var server = services.GetRequiredService<ReturnDefaultTester>();

            var weakRef = await GetInvalidatedRemoteComputed(client, server);
            Collect();
            weakRef.TryGetTarget(out _).Should().BeTrue("the keep-alive period hasn't expired yet");

            await TestExt.When(() => {
                Collect();
                weakRef.TryGetTarget(out _).Should().BeFalse("the keep-alive period has expired");
            }, Timeout);
        }
        finally {
            ComputedRegistry.Settings.PostInvalidateKeepAliveDurationForRemoteComputedWithCacheOrReturnDefaultCacheMode = oldKeepAliveDuration;
        }
    }

    // Private methods

    private async Task<List<string?>> GetSequence(bool mustCollect, bool mustHoldRemoteComputed, bool useCacheMode)
    {
        await using var services = CreateServices();
        var client = services.RpcHub().GetClient<IReturnDefaultTester>();
        var server = services.GetRequiredService<ReturnDefaultTester>();
        var consumer = services.GetRequiredService<ReturnDefaultConsumer>();

        var values = new List<string?>();
        using var cts = new CancellationTokenSource();
        var cD = await Computed.Capture(() => consumer.Get(useCacheMode));
        var observeTask = Task.Run(async () => {
            await foreach (var c in cD.Changes(FixedDelayer.NextTick, cts.Token))
                lock (values)
                    values.Add(c.Value);
        }, CancellationToken.None);
        await TestExt.When(() => {
            lock (values)
                values.Should().Contain("v-1");
        }, Timeout);

        var remoteComputed = mustHoldRemoteComputed
            ? Computed.GetExisting(() => client.GetDefault("1"))
            : null;
        if (mustCollect)
            consumer.BeforeGet = Collect;
        server.Set("1", "b");
        await TestExt.When(() => {
            lock (values)
                values.Should().Contain("b");
        }, Timeout);
        cts.Cancel();
        await observeTask.SilentAwait();
        GC.KeepAlive(remoteComputed);

        lock (values) {
            Out.WriteLine($"Sequence: {values.Select(x => x ?? "<null>").ToDelimitedString(", ")}");
            return values.ToList();
        }
    }

    // A separate method, so no local of the caller's state machine keeps the computed alive
    private static async Task<WeakReference<Computed>> GetInvalidatedRemoteComputed(
        IReturnDefaultTester client, ReturnDefaultTester server)
    {
        var computed = (RemoteComputed<string>)await Computed.Capture(() => client.GetDefault("1"));
        await computed.WhenSynchronized.WaitAsync(Timeout);
        computed = (RemoteComputed<string>)Computed.GetExisting(() => client.GetDefault("1"))!;
        computed.Value.Should().Be("v-1");
        server.Set("1", "b");
        await computed.WhenInvalidated().WaitAsync(Timeout);
        return new WeakReference<Computed>(computed);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public class ReturnDefaultConsumer(IServiceProvider services) : IComputeService
    {
        private readonly IReturnDefaultTester _client = services.RpcHub().GetClient<IReturnDefaultTester>();

        public Action? BeforeGet { get; set; }

        [ComputeMethod]
        public virtual async Task<string?> Get(bool useCacheMode, CancellationToken cancellationToken = default)
        {
            // Voxt's "gate": the recompute awaits something before it reaches the remote method
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            BeforeGet?.Invoke();
            return useCacheMode
                ? await _client.Get("1", cancellationToken).ConfigureAwait(false)
                : await _client.GetDefault("1", cancellationToken).ConfigureAwait(false);
        }
    }
}
