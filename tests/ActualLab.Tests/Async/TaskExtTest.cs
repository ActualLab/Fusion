using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Async;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class TaskExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void FromDefaultResultTest()
    {
        // Task
        ((Task<int>)TaskExt.FromDefaultResult(typeof(int))).Result.Should().Be(0);
        ((Task<bool>)TaskExt.FromDefaultResult(typeof(bool))).Result.Should().BeFalse();
        ((Task<string>)TaskExt.FromDefaultResult(typeof(string))).Result.Should().BeNull();

        // ValueTask
        ((ValueTask<int>)ValueTaskExt.FromDefaultResult(typeof(int))).Result.Should().Be(0);
        ((ValueTask<bool>)ValueTaskExt.FromDefaultResult(typeof(bool))).Result.Should().BeFalse();
        ((ValueTask<string>)ValueTaskExt.FromDefaultResult(typeof(string))).Result.Should().BeNull();
    }

    [Fact]
    public async Task ToXxxResultTest()
    {
        using var cts = new CancellationTokenSource(200);
        var t1 = Task.Delay(50);
        var t2 = IntDelayOne(50);
        var t3 = FailDelay(50);
        var t4 = FailIntDelay(50);
        var t5 = Task.Delay(500).WaitAsync(cts.Token);
        var t6 = IntDelayOne(500).WaitAsync(cts.Token);

        Assert.Throws<InvalidOperationException>(() => t1.ToResultSynchronously());
        Assert.Throws<InvalidOperationException>(() => t2.ToResultSynchronously());
        Assert.Throws<InvalidOperationException>(() => t3.ToResultSynchronously());
        Assert.Throws<InvalidOperationException>(() => t4.ToResultSynchronously());
        Assert.Throws<InvalidOperationException>(() => t5.ToResultSynchronously());
        Assert.Throws<InvalidOperationException>(() => t6.ToResultSynchronously());
        Assert.Throws<InvalidOperationException>(() => t1.ToTypedResultSynchronously(typeof(void)));
        Assert.Throws<InvalidOperationException>(() => t2.ToTypedResultSynchronously(typeof(int)));
        Assert.Throws<InvalidOperationException>(() => t3.ToTypedResultSynchronously(typeof(void)));
        Assert.Throws<InvalidOperationException>(() => t4.ToTypedResultSynchronously(typeof(int)));
        Assert.Throws<InvalidOperationException>(() => t5.ToTypedResultSynchronously(typeof(void)));
        Assert.Throws<InvalidOperationException>(() => t6.ToTypedResultSynchronously(typeof(int)));
        Assert.Throws<InvalidOperationException>(() => t1.ToUntypedResultSynchronously(typeof(void)));
        Assert.Throws<InvalidOperationException>(() => t2.ToUntypedResultSynchronously(typeof(int)));
        Assert.Throws<InvalidOperationException>(() => t3.ToUntypedResultSynchronously(typeof(void)));
        Assert.Throws<InvalidOperationException>(() => t4.ToUntypedResultSynchronously(typeof(int)));
        Assert.Throws<InvalidOperationException>(() => t5.ToUntypedResultSynchronously(typeof(void)));
        Assert.Throws<InvalidOperationException>(() => t6.ToUntypedResultSynchronously(typeof(int)));
        Assert.Throws<InvalidOperationException>(() => t1.GetUntypedResultSynchronously(typeof(void)));
        Assert.Throws<InvalidOperationException>(() => t2.GetUntypedResultSynchronously(typeof(int)));
        Assert.Throws<InvalidOperationException>(() => t3.GetUntypedResultSynchronously(typeof(void)));
        Assert.Throws<InvalidOperationException>(() => t4.GetUntypedResultSynchronously(typeof(int)));
        Assert.Throws<InvalidOperationException>(() => t5.GetUntypedResultSynchronously(typeof(void)));
        Assert.Throws<InvalidOperationException>(() => t6.GetUntypedResultSynchronously(typeof(int)));

        (await t1.ToResultAsync()).HasValue.Should().BeTrue();
        (await t2.ToResultAsync()).Value.Should().Be(1);
        (await t3.ToResultAsync()).Error.Should().BeOfType<InvalidOperationException>();
        (await t4.ToResultAsync()).Error.Should().BeOfType<InvalidOperationException>();
        (await t5.ToResultAsync()).Error.Should().BeAssignableTo<OperationCanceledException>();
        (await t6.ToResultAsync()).Error.Should().BeAssignableTo<OperationCanceledException>();

        t1.ToResultSynchronously().HasValue.Should().BeTrue();
        t2.ToResultSynchronously().Value.Should().Be(1);
        t3.ToResultSynchronously().Error.Should().BeOfType<InvalidOperationException>();
        t4.ToResultSynchronously().Error.Should().BeOfType<InvalidOperationException>();
        t5.ToResultSynchronously().Error.Should().BeAssignableTo<OperationCanceledException>();
        t6.ToResultSynchronously().Error.Should().BeAssignableTo<OperationCanceledException>();

        t1.ToTypedResultSynchronously(typeof(void)).Should().Be(Result.New<Unit>(default));
        t2.ToTypedResultSynchronously(typeof(int)).Should().Be(Result.New(1));
        t3.ToTypedResultSynchronously(typeof(void)).Error.Should().BeOfType<InvalidOperationException>();
        t4.ToTypedResultSynchronously(typeof(int)).Error.Should().BeOfType<InvalidOperationException>();
        t5.ToTypedResultSynchronously(typeof(void)).Error.Should().BeAssignableTo<OperationCanceledException>();
        t6.ToTypedResultSynchronously(typeof(int)).Error.Should().BeAssignableTo<OperationCanceledException>();

        t1.ToUntypedResultSynchronously(typeof(void)).Should().Be(Result.NewUntyped(null));
        t2.ToUntypedResultSynchronously(typeof(int)).Should().Be(Result.NewUntyped(1));
        t3.ToUntypedResultSynchronously(typeof(void)).Error.Should().BeOfType<InvalidOperationException>();
        t4.ToUntypedResultSynchronously(typeof(int)).Error.Should().BeOfType<InvalidOperationException>();
        t5.ToUntypedResultSynchronously(typeof(void)).Error.Should().BeAssignableTo<OperationCanceledException>();
        t6.ToUntypedResultSynchronously(typeof(int)).Error.Should().BeAssignableTo<OperationCanceledException>();

        t1.GetUntypedResultSynchronously(typeof(void)).Should().Be(null);
        t2.GetUntypedResultSynchronously(typeof(int)).Should().Be(1);
        Assert.ThrowsAny<InvalidOperationException>(() => t3.GetUntypedResultSynchronously(typeof(void)));
        Assert.ThrowsAny<InvalidOperationException>(() => t4.GetUntypedResultSynchronously(typeof(int)));
        Assert.ThrowsAny<OperationCanceledException>(() => t5.GetUntypedResultSynchronously(typeof(void)));
        Assert.ThrowsAny<OperationCanceledException>(() => t6.GetUntypedResultSynchronously(typeof(int)));
    }

    [Fact]
    public async Task WaitAsyncTest1()
    {
        using var cts = new CancellationTokenSource(100);
        var t0 = Task.Delay(50).WaitAsync(TimeSpan.FromMilliseconds(200), cts.Token);
        var t1 = Task.Delay(500).WaitAsync(TimeSpan.FromMilliseconds(50), cts.Token);
        var t2 = Task.Delay(500).WaitAsync(TimeSpan.FromMilliseconds(200), cts.Token);
        var t3 = FailDelay(10).WaitAsync(TimeSpan.FromMilliseconds(200), cts.Token);

        await t0;
        await Assert.ThrowsAnyAsync<TimeoutException>(() => t1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t2);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => t3);
    }

    [Fact]
    public async Task WaitAsyncTest2()
    {
        using var cts = new CancellationTokenSource(100);

        var t0 = IntDelayOne(50).WaitAsync(TimeSpan.FromMilliseconds(200), cts.Token);
        var t1 = IntDelayOne(500).WaitAsync(TimeSpan.FromMilliseconds(50), cts.Token);
        var t2 = IntDelayOne(500).WaitAsync(TimeSpan.FromMilliseconds(200), cts.Token);
        var t3 = FailIntDelay(10).WaitAsync(TimeSpan.FromMilliseconds(200), cts.Token);

        (await t0).Should().Be(1);
        await Assert.ThrowsAnyAsync<TimeoutException>(() => t1);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t2);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => t3);
    }

    [Fact]
    public async Task WaitResultAsyncTest1()
    {
        using var cts = new CancellationTokenSource(100);
        var t0 = Task.Delay(50).WaitResultAsync(TimeSpan.FromMilliseconds(200), cts.Token);
        var t1 = Task.Delay(500).WaitResultAsync(TimeSpan.FromMilliseconds(50), cts.Token);
        var t2 = Task.Delay(500).WaitResultAsync(TimeSpan.FromMilliseconds(200), cts.Token);
        var t3 = FailDelay(10).WaitResultAsync(TimeSpan.FromMilliseconds(200), cts.Token);

        (await t0).HasValue.Should().BeTrue();
        (await t1).HasValue.Should().BeFalse();
        (await t2).Error.Should().BeAssignableTo<OperationCanceledException>();
        (await t3).Error.Should().BeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public async Task WaitResultAsyncTest2()
    {
        using var cts = new CancellationTokenSource(100);

        var t0 = IntDelayOne(50).WaitResultAsync(TimeSpan.FromMilliseconds(200), cts.Token);
        var t1 = IntDelayOne(500).WaitResultAsync(TimeSpan.FromMilliseconds(50), cts.Token);
        var t2 = IntDelayOne(500).WaitResultAsync(TimeSpan.FromMilliseconds(200), cts.Token);
        var t3 = FailIntDelay(10).WaitResultAsync(TimeSpan.FromMilliseconds(200), cts.Token);

        (await t0).Value.Should().Be(1);
        (await t1).HasValue.Should().BeFalse();
        (await t2).Error.Should().BeAssignableTo<OperationCanceledException>();
        (await t3).Error.Should().BeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public async Task CollectTest()
    {
        var tests = new List<Task>();
        for (var concurrencyLevel = 0; concurrencyLevel <= 4; concurrencyLevel++)
            for (var size = 0; size < 50; size++) {
                tests.Add(Test(concurrencyLevel, size));
                tests.Add(UntypedTest(concurrencyLevel, size));
            }

        await Task.WhenAll(tests);

        async Task Test(int cl, int size)
        {
            var rnd = new Random(cl * size);
            var seeds = Enumerable.Range(0, size).Select(_ => rnd.Next()).ToArray();
            var tasks = seeds.Select(seed => RandomIntDelay(seed, 200));

            // ReSharper disable once PossibleMultipleEnumeration
            var collectTask = tasks.Collect(cl);
            // ReSharper disable once PossibleMultipleEnumeration
            var whenAllTask = Task.WhenAll(tasks);
            var collect = await collectTask.ResultAwait();
            var whenAll = await whenAllTask.ResultAwait();

            collectTask.IsCompletedSuccessfully().Should().Be(whenAllTask.IsCompletedSuccessfully());
            if (whenAllTask.IsCompletedSuccessfully()) {
                var s1 = collect.Value.ToDelimitedString();
                var s2 = whenAll.Value.ToDelimitedString();
                Out.WriteLine($"CL={cl}, Size={size} -> {s1}");
                s1.Should().Be(s2);
            }
            else {
                Out.WriteLine($"CL={cl}, Size={size} -> error (ok)");
            }
        }

        async Task UntypedTest(int cl, int size)
        {
            var rnd = new Random(cl * size);
            var seeds = Enumerable.Range(0, size).Select(_ => rnd.Next()).ToArray();
            var tasks = seeds.Select(seed => (Task)RandomIntDelay(seed, 200));

            // ReSharper disable once PossibleMultipleEnumeration
            var collectTask = tasks.Collect(cl);
            // ReSharper disable once PossibleMultipleEnumeration
            var whenAllTask = Task.WhenAll(tasks);
            await collectTask.SilentAwait();
            await whenAllTask.SilentAwait();

            collectTask.IsCompletedSuccessfully().Should().Be(whenAllTask.IsCompletedSuccessfully());
        }
    }

    [Fact]
    public async Task CollectResultTest()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Test(0));
        await Test(-1);

        Task<Result<int>[]> Test(int cancelAt) {
            var cts = new CancellationTokenSource();
            var range = Enumerable.Range(0, 1000);
            var rnd = new Random();
            var totalTaskCount = 0;
            var seq = range.Select(async i => {
                var taskCount = Interlocked.Increment(ref totalTaskCount);
                taskCount.Should().BeLessThan(105);
                try {
                    var delay = rnd.Next(250);
                    if (i == cancelAt)
                        cts.Cancel();
                    await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                }
                finally {
                    Interlocked.Decrement(ref totalTaskCount);
                }
                return i;
            });
            return seq.CollectResults(100, cts.Token);
        }
    }

    // Private methods

    private Task<int> RandomIntDelay(int seed, int maxDelay)
    {
        var delay = seed % maxDelay;
        return delay == 0
            ? FailIntDelay(seed * 353 % maxDelay)
            : IntDelay(delay);
    }

    private async Task<int> IntDelay(int delay)
    {
        await Task.Delay(delay);
        return delay;
    }

    private async Task FailDelay(int delay)
    {
        await Task.Delay(delay);
        throw new InvalidOperationException();
    }

    private async Task<int> IntDelayOne(int delay)
    {
        await Task.Delay(delay);
        return 1;
    }

    private async Task<int> FailIntDelay(int delay)
    {
        await Task.Delay(delay);
        throw new InvalidOperationException();
    }
}
