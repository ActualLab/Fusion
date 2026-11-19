using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Async;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class BatchProcessorTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task BasicTest()
    {
        var batchIndex = 0;
        await using var processor = new BatchProcessor<int, (int BatchIndex, int Value)>() {
            BatchSize = 3,
            WorkerPolicy = new BatchProcessorWorkerPolicy {
                MinWorkerCount = 1,
                MaxWorkerCount = 3,
            },
            Implementation = async (batch, cancellationToken) => {
                var bi = Interlocked.Increment(ref batchIndex);
                await Task.Delay(100).ConfigureAwait(false);
                foreach (var item in batch) {
                    if (item.Input == -1000)
                        throw new InvalidOperationException();
                    item.TrySetResult((bi, item.Input));
                }
            }
        };

        async Task Reset() {
            // ReSharper disable once AccessToDisposedClosure
            await processor.Reset();
            Interlocked.Exchange(ref batchIndex, 0);
        }

        // Batch formation tests
        await Reset();
        var r = await processor.Process(1);
        r.Should().Be((1, 1));

        await Reset();
        var tasks = Enumerable.Range(0, 4).Select(i => processor.Process(i)).ToArray();
        await Task.WhenAll(tasks);
        tasks.Count(t => t.Result.BatchIndex == 1).Should().Be(1);
        tasks.Count(t => t.Result.BatchIndex == 2).Should().Be(3);
        tasks.Select(t => t.Result.Value).Should().BeEquivalentTo(Enumerable.Range(0, 4));

        await Reset();
        tasks = Enumerable.Range(0, 6).Select(i => processor.Process(i)).ToArray();
        await Task.WhenAll(tasks);
        tasks.Count(t => t.Result.BatchIndex == 1).Should().Be(1);
        tasks.Count(t => t.Result.BatchIndex > 1).Should().Be(5);
        tasks.Select(t => t.Result.Value).Should().BeEquivalentTo(Enumerable.Range(0, 6));

        await Reset();
        tasks = Enumerable.Range(0, 10).Select(i => processor.Process(i)).ToArray();
        await Task.WhenAll(tasks);
        tasks.Count(t => t.Result.BatchIndex == 1).Should().Be(1);
        tasks.GroupBy(t => t.Result.BatchIndex).All(g => g.Count() <= 3).Should().BeTrue();
        tasks.Select(t => t.Result.Value).Should().BeEquivalentTo(Enumerable.Range(0, 10));

        // Cancellation test
        await Reset();
        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();
        tasks = Enumerable.Range(0, 4)
            .Select(i => processor.Process(i, i % 2 == 0 ? cts1.Token : cts2.Token))
            .Select(t => t.SuppressCancellation())
            .ToArray();
        await Delay(0.02);
        cts1.Cancel();
        await Task.WhenAll(tasks);
        tasks.Count(t => t.Result == (0, 0)).Should().Be(1);
        tasks.Count(t => t.Result.BatchIndex == 1).Should().Be(1);
        tasks.Count(t => t.Result.BatchIndex == 2).Should().Be(2);

        // Error test
        await Reset();
        tasks = new [] {0, 1, 2, -1000}
            .Select(i => processor.Process(i, default))
            .ToArray();
        try {
            await Task.WhenAll(tasks);
            true.Should().BeFalse("No exception was thrown.");
        }
        catch (InvalidOperationException) {
            // Intended
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task IdleCollectorShouldNotUseShortCycleAtMinWorkerCount()
    {
        // arrange
        var workerPolicy = new AlwaysShrinkWorkerPolicy() {
            CollectorCycle = TimeSpan.FromSeconds(0.5),
        };
        await using var processor = new BatchProcessor<int, int>() {
            WorkerPolicy = workerPolicy,
            Implementation = (batch, _) => {
                foreach (var item in batch)
                    item.TrySetResult(item.Input);
                return Task.CompletedTask;
            },
        };
        await processor.Process(0);

        // act
        await Delay(2.2);

        // assert
        processor.GetWorkerCount().Should().Be(1);
        workerPolicy.CallCount.Should().BeLessThanOrEqualTo(5,
            "with nothing to remove the collector must run once per CollectorCycle, not every Cooldown");
    }

    [Fact(Timeout = 30_000)]
    public async Task ActiveProcessorAtMinWorkerCountShouldScaleUpQuickly()
    {
        // arrange
        var isSlow = 0;
        await using var processor = new BatchProcessor<int, int>() {
            BatchSize = 15,
            WorkerPolicy = new BatchProcessorWorkerPolicy {
                MinWorkerCount = 1,
                MaxWorkerCount = 4,
                CollectorCycle = TimeSpan.FromSeconds(2),
            },
            Implementation = async (batch, cancellationToken) => {
                if (Volatile.Read(ref isSlow) != 0)
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                foreach (var item in batch)
                    item.TrySetResult(item.Input);
            },
        };
        var startedAt = CpuTimestamp.Now;
        while (startedAt.Elapsed < TimeSpan.FromSeconds(2.5)) {
            _ = processor.Process(0);
            await Delay(0.1);
        }

        // act
        Volatile.Write(ref isSlow, 1);
        var slowSince = CpuTimestamp.Now;
        while (processor.GetWorkerCount() < 2 && slowSince.Elapsed < TimeSpan.FromSeconds(3)) {
            _ = processor.Process(0);
            await Delay(0.02);
        }
        var scaleUpTime = slowSince.Elapsed;

        // assert
        scaleUpTime.Should().BeLessThan(TimeSpan.FromSeconds(0.8),
            "a processor with work must keep checking its queue often even at MinWorkerCount");
    }

    [Theory(Timeout = 30_000)]
    [InlineData(2.5)] // The first collector cycle has just found the queue idle
    [InlineData(4.8)] // The idle collector's wait has timed out once, so it waits again
    public async Task IdleProcessorShouldScaleUpQuicklyOnceLoadStarts(double idleDuration)
    {
        // arrange
        var batchDelay = 100;
        await using var processor = CreateDbLikeProcessor(() => batchDelay, TimeSpan.FromSeconds(2));
        await processor.Process(0);
        await Delay(idleDuration);

        // act
        var scaleUpTime = await LoadUntilSecondWorker(processor);

        // assert
        scaleUpTime.Should().BeLessThan(TimeSpan.FromSeconds(0.8),
            "the first item dequeued after idle must wake the collector up");
    }

    [Fact(Timeout = 60_000)]
    public async Task EveryLoadBurstAfterIdleShouldScaleUpQuickly()
    {
        // arrange
        var batchDelay = 100;
        await using var processor = CreateDbLikeProcessor(() => batchDelay, TimeSpan.FromSeconds(2));
        await processor.Process(0);
        await Delay(2.5);

        foreach (var idleDuration in new[] { 0.3, 0.45, 0.7, 1.1, 2.3 }) {
            // act
            var scaleUpTime = await LoadUntilSecondWorker(processor);
            await WaitForMinWorkerCount(processor);
            await Delay(idleDuration);

            // assert
            Out.WriteLine($"Idle {idleDuration:F2}s -> scale-up in {scaleUpTime.TotalSeconds:F3}s");
            scaleUpTime.Should().BeLessThan(TimeSpan.FromSeconds(0.8),
                "every load burst after idle must be noticed by the collector on its short cycle");
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task FastLoadAfterIdleShouldNotAddWorkers()
    {
        // arrange
        var batchDelay = 0;
        await using var processor = CreateDbLikeProcessor(() => batchDelay, TimeSpan.FromSeconds(2));
        await processor.Process(0);
        await Delay(2.5);

        // act
        var maxWorkerCount = 0;
        var loadSince = CpuTimestamp.Now;
        while (loadSince.Elapsed < TimeSpan.FromSeconds(1.5)) {
            await processor.Process(0);
            maxWorkerCount = Math.Max(maxWorkerCount, processor.GetWorkerCount());
            await Delay(0.02);
        }

        // assert
        maxWorkerCount.Should().Be(1, "a waking collector must not add workers a fast queue doesn't need");
    }

    [Fact(Timeout = 30_000)]
    public async Task CollectorShouldReturnToCollectorCycleOnceLoadEnds()
    {
        // arrange
        var workerPolicy = new CountingWorkerPolicy() {
            MinWorkerCount = 1,
            MaxWorkerCount = 4,
            CollectorCycle = TimeSpan.FromSeconds(0.5),
        };
        var batchDelay = 100;
        await using var processor = CreateDbLikeProcessor(() => batchDelay, workerPolicy);
        await processor.Process(0);
        await Delay(1);
        await LoadUntilSecondWorker(processor);
        await WaitForMinWorkerCount(processor);
        await Delay(0.5);

        // act
        var callCount = workerPolicy.CallCount;
        await Delay(2.2);
        callCount = workerPolicy.CallCount - callCount;

        // assert
        processor.GetWorkerCount().Should().Be(1);
        callCount.Should().BeLessThanOrEqualTo(5,
            "once the load ends, an idle collector must run once per CollectorCycle, not every Cooldown");
    }

    [Fact(Timeout = 30_000)]
    public async Task DisposeShouldStopIdleCollector()
    {
        // arrange
        var processor = new CollectorExposingBatchProcessor() {
            WorkerPolicy = new BatchProcessorWorkerPolicy {
                CollectorCycle = TimeSpan.FromSeconds(5),
            },
            Implementation = (batch, _) => {
                foreach (var item in batch)
                    item.TrySetResult(item.Input);
                return Task.CompletedTask;
            },
        };
        await processor.Process(0);
        await Delay(5.5); // The idle collector waits for a wake-up now

        // act
        await processor.DisposeAsync();

        // assert
        var collectorTask = processor.CollectorTask!;
        await collectorTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact(Timeout = 30_000)]
    public async Task WorkerRampUpTest()
    {
        if (TestRunnerInfo.IsBuildAgent())
            return;

        var batchDelay = 100;

        var services = CreateLoggingServices(Out, useDebugLog: false);
        await using var processor = new BatchProcessor<int, int>() {
            BatchSize = 10,
            WorkerPolicy = new BatchProcessorWorkerPolicy() {
                MinWorkerCount = 1,
                MaxWorkerCount = 100,
                CollectorCycle = TimeSpan.FromSeconds(1),
            },
            Implementation = async (batch, cancellationToken) => {
                await Task.Delay(batchDelay, cancellationToken).ConfigureAwait(false);
                foreach (var item in batch)
                    item.TrySetResult(item.Input);
            },
            Log = services.LogFor<BatchProcessor<int, int>>(),
        };

        processor.GetWorkerCount().Should().Be(0);
        processor.GetPlannedWorkerCount().Should().Be(0);
        await processor.Process(0);
        processor.GetWorkerCount().Should().Be(1);
        processor.GetPlannedWorkerCount().Should().Be(1);

        batchDelay = 10;
        await Test(1000, 5);

        batchDelay = 100;
        await Test(1000, 10);
        await Test(10_000, 60);
        return;

        async Task Test(int taskCount, int minExpectedWorkerCount) {
            WriteLine($"Task count: {taskCount}, batch delay: {batchDelay}");
            var tasks = new Task<int>[taskCount];
            for (var i = 0; i < tasks.Length; i++)
                tasks[i] = processor.Process(i);

            while (true) {
                await Delay(0.5);
                WriteLine($"WorkerCount: {processor.GetWorkerCount()}");
                if (processor.GetWorkerCount() >= minExpectedWorkerCount)
                    break;
            }
            while (true) {
                await Delay(0.5);
                WriteLine($"WorkerCount: {processor.GetWorkerCount()}");
                if (processor.GetWorkerCount() == 1)
                    break;
            }
            await Task.WhenAll(tasks);
            tasks.Where((t, i) => t.Result != i).Count().Should().Be(0);
            WriteLine("");
        }
    }

    // Nested types

    // DbEntityResolver's setup: batches of 15 items, a single worker at MinWorkerCount
    private static BatchProcessor<int, int> CreateDbLikeProcessor(Func<int> batchDelay, TimeSpan collectorCycle)
        => CreateDbLikeProcessor(batchDelay, new BatchProcessorWorkerPolicy {
            MinWorkerCount = 1,
            MaxWorkerCount = 4,
            CollectorCycle = collectorCycle,
        });

    private static BatchProcessor<int, int> CreateDbLikeProcessor(
        Func<int> batchDelay, IBatchProcessorWorkerPolicy workerPolicy)
        => new() {
            BatchSize = 15,
            WorkerPolicy = workerPolicy,
            Implementation = async (batch, cancellationToken) => {
                var delay = batchDelay.Invoke();
                if (delay > 0)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                foreach (var item in batch)
                    item.TrySetResult(item.Input);
            },
        };

    // Queues an item every 20 ms: with 100 ms batches one worker falls behind, but its batches never fill up
    private static async Task<TimeSpan> LoadUntilSecondWorker(BatchProcessor<int, int> processor)
    {
        var loadSince = CpuTimestamp.Now;
        while (processor.GetWorkerCount() < 2 && loadSince.Elapsed < TimeSpan.FromSeconds(3)) {
            _ = processor.Process(0);
            await Task.Delay(20).ConfigureAwait(false);
        }
        return loadSince.Elapsed;
    }

    private static async Task WaitForMinWorkerCount(BatchProcessor<int, int> processor)
    {
        var minWorkerCount = processor.WorkerPolicy.MinWorkerCount;
        while (processor.GetWorkerCount() > minWorkerCount || processor.GetPlannedWorkerCount() > minWorkerCount)
            await Task.Delay(50).ConfigureAwait(false);
    }

    private sealed class CollectorExposingBatchProcessor : BatchProcessor<int, int>
    {
        public Task? CollectorTask => WorkerCollectorTask;
    }

    private sealed record CountingWorkerPolicy : BatchProcessorWorkerPolicy
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override int GetWorkerCountDelta(TimeSpan minQueueTime)
        {
            Interlocked.Increment(ref _callCount);
            return base.GetWorkerCountDelta(minQueueTime);
        }
    }

    private sealed record AlwaysShrinkWorkerPolicy : BatchProcessorWorkerPolicy
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public override int GetWorkerCountDelta(TimeSpan minQueueTime)
        {
            Interlocked.Increment(ref _callCount);
            return -1;
        }
    }
}
