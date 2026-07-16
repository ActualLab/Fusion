using System.Reflection;
using ActualLab.Fusion.Internal;
using ActualLab.Fusion.Client.Caching;
using ActualLab.Rpc.Caching;

namespace ActualLab.Tests.Audit;

public class FusionAuditRegressionTest
{
    [Fact]
    public async Task ComputedRegistryMustReplaceGraphPruner()
    {
        var original = ComputedRegistry.GraphPruner;
        var autoActivate = ComputedGraphPruner.Settings.AutoActivate;
        ComputedGraphPruner.Settings.AutoActivate = false;
        var replacement = new ComputedGraphPruner();
        try {
            var (isChanged, previous) = ChangeGraphPruner(replacement);
            isChanged.Should().BeTrue();
            previous.Should().BeSameAs(original);
            ComputedRegistry.GraphPruner.Should().BeSameAs(replacement);
        }
        finally {
            ChangeGraphPruner(original);
            ComputedGraphPruner.Settings.AutoActivate = autoActivate;
            await replacement.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task FlushMustWaitForPersistentWrite()
    {
        var services = new ServiceCollection();
        services.AddFusion();
        await using var serviceProvider = services.BuildServiceProvider();
        var cache = new GatedFlushingCache(serviceProvider);
        cache.Set(new RpcCacheKey("method", default), new RpcCacheValue(new byte[] { 1 }, ""));

        var flushTask = cache.Flush();
        await cache.WhenFlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        var completedEarly = flushTask.IsCompleted;
        cache.AllowFlush.TrySetResult();
        await cache.WhenFlushCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        completedEarly.Should().BeFalse();
    }

    [Fact]
    public async Task FlushMustPropagatePersistentWriteFailure()
    {
        var services = new ServiceCollection();
        services.AddFusion();
        await using var serviceProvider = services.BuildServiceProvider();
        var error = new InvalidOperationException("Failed to persist.");
        var cache = new GatedFlushingCache(serviceProvider) {
            FlushError = error,
        };
        cache.Set(new RpcCacheKey("method", default), new RpcCacheValue(new byte[] { 1 }, ""));

        var flushTask = cache.Flush();
        await cache.WhenFlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        cache.AllowFlush.TrySetResult();

        var action = () => flushTask;
        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(error);
    }

    private static (bool IsChanged, ComputedGraphPruner? Previous) ChangeGraphPruner(ComputedGraphPruner? value)
    {
        var method = typeof(ComputedRegistry).GetMethod(
            "ChangeGraphPruner",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        object?[] arguments = [value, null];
        var result = (bool)method.Invoke(null, arguments)!;
        return (result, (ComputedGraphPruner?)arguments[1]);
    }

    private sealed class GatedFlushingCache(IServiceProvider services)
        : FlushingRemoteComputedCache(new Options(""), services)
    {
        public TaskCompletionSource WhenFlushStarted { get; } = TaskCompletionSourceExt.New();
        public TaskCompletionSource AllowFlush { get; } = TaskCompletionSourceExt.New();
        public TaskCompletionSource WhenFlushCompleted { get; } = TaskCompletionSourceExt.New();
        public Exception? FlushError { get; init; }

        protected override ValueTask<RpcCacheValue?> Fetch(RpcCacheKey key, CancellationToken cancellationToken)
            => default;

        protected override async Task Flush(Dictionary<RpcCacheKey, RpcCacheValue?> flushingQueue)
        {
            WhenFlushStarted.TrySetResult();
            try {
                await AllowFlush.Task.ConfigureAwait(false);
            }
            finally {
                WhenFlushCompleted.TrySetResult();
            }
            if (FlushError is { } error)
                throw error;
        }

        public override Task Clear(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
