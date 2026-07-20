namespace ActualLab.Tests.Async;

public class TaskCoalescerTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task SequentialRunsTest()
    {
        var runCount = 0;
        var coalescer = new TaskCoalescer(async () => {
            Interlocked.Increment(ref runCount);
            await Task.Yield();
        });

        await coalescer.Invoke();
        await coalescer.Invoke();
        await coalescer.Invoke();
        runCount.Should().Be(3); // No concurrency = no coalescing
    }

    [Fact]
    public async Task CoalescingTest()
    {
        var runCount = 0;
        var gate = TaskCompletionSourceExt.New<Unit>();
        var coalescer = new TaskCoalescer(async () => {
            Interlocked.Increment(ref runCount);
            await gate.Task.ConfigureAwait(false);
        });

        var t1 = coalescer.Invoke();
        var t2 = coalescer.Invoke();
        var t3 = coalescer.Invoke();
        runCount.Should().Be(1);
        t2.Should().BeSameAs(t3); // Requests behind an in-flight run share the queued one
        t2.Should().NotBeSameAs(t1);
        coalescer.LastTask.Should().BeSameAs(t2);

        gate.SetResult(default);
        await t3;
        await t1;
        runCount.Should().Be(2); // The whole burst is served by two runs
    }

    [Fact]
    public async Task ConcurrentBurstTest()
    {
        var runCount = 0;
        var gate = TaskCompletionSourceExt.New<Unit>();
        var coalescer = new TaskCoalescer(async () => {
            Interlocked.Increment(ref runCount);
            await gate.Task.ConfigureAwait(false);
        });

        var t1 = coalescer.Invoke();
        var tasks = Enumerable.Range(0, 100)
            .AsParallel()
            .Select(_ => coalescer.Invoke())
            .ToArray();
        runCount.Should().Be(1);
        tasks.Should().AllSatisfy(t => t.Should().BeSameAs(tasks[0]));

        gate.SetResult(default);
        await Task.WhenAll(tasks);
        await t1;
        runCount.Should().Be(2);
    }

    [Fact]
    public async Task ErrorPropagationTest()
    {
        var runCount = 0;
        var mustFail = true;
        var coalescer = new TaskCoalescer(async () => {
            Interlocked.Increment(ref runCount);
            await Task.Yield();
            if (mustFail)
                throw new InvalidOperationException("Simulated");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => coalescer.Invoke());
        mustFail = false;
        await coalescer.Invoke(); // A faulted active task must not block new runs
        runCount.Should().Be(2);
    }

    [Fact]
    public async Task QueuedRunErrorPropagationTest()
    {
        var runCount = 0;
        var gate = TaskCompletionSourceExt.New<Unit>();
        var coalescer = new TaskCoalescer(async () => {
            var runIndex = Interlocked.Increment(ref runCount);
            await gate.Task.ConfigureAwait(false);
            if (runIndex == 2)
                throw new InvalidOperationException("Simulated");
        });

        var t1 = coalescer.Invoke();
        var t2 = coalescer.Invoke();
        gate.SetResult(default);
        await t1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => t2);

        await coalescer.Invoke(); // And a faulted queued task must not block new runs either
        runCount.Should().Be(3);
    }
}
