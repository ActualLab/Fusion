using ActualLab.Rpc;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Platform;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class TaskTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThrowRpcRerouteExceptionTest(bool useDelay)
    {
        var task = Failing().ContinueWith(t => {
            t.GetAwaiter().GetResult();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await Assert.ThrowsAsync<RpcRerouteException>(() => task);
        return;

        async Task<bool> Failing()
        {
            if (useDelay)
                await Task.Delay(100).ConfigureAwait(false);
            throw RpcRerouteException.MustReroute();
        }
    }

    [Fact]
    public async Task DelayTest()
    {
        var tasks = new List<Task<TimeSpan>>();
        var requestedDelay = TimeSpan.FromMilliseconds(5);
        var sw = new SpinWait();
        for (var iteration = 0; iteration < 10; iteration++) {
            var now = CpuTimestamp.Now;
            var initialDelay = TimeSpan.FromMilliseconds(new Random().NextDouble() * 3);
            while (now.Elapsed < initialDelay)
                sw.SpinOnce();

            for (var i = 0; i < 17; i++) {
                tasks.Add(MeasureDelay(requestedDelay));
                now = CpuTimestamp.Now;
                while (now.Elapsed < TimeSpan.FromMilliseconds(1))
                    sw.SpinOnce();
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
            var minDelay = tasks.Select(t => t.Result).Min();
            var maxDelay = tasks.Select(t => t.Result).Max();
            WriteLine($"Delays: min = {minDelay.TotalMilliseconds}ms, max = {maxDelay.TotalMilliseconds}ms");
        }
    }

    [Fact(Skip = "Benchmark")]
    public void GetResultBenchmark()
    {
        const int Iterations = 2000_000_000;
        Task<int> completedTask = Task.FromResult(123);

        // Warm-up (JIT + cache)
        for (int i = Iterations/10; i >= 0; i--) {
            _ = completedTask.Result;
            _ = completedTask.GetAwaiter().GetResult();
        }

        // Benchmark task.Result
        var start = CpuTimestamp.Now;
        for (int i = Iterations; i > 0; i--)
            _ = completedTask.Result;
        var rTime = start.Elapsed;

        // Benchmark task.GetAwaiter().GetResult()
        start = CpuTimestamp.Now;
        for (int i = Iterations; i > 0; i--)
            _ = completedTask.GetAwaiter().GetResult();
        var gwgrTime = start.Elapsed;

        WriteLine($".Result                   : {rTime.ToShortString()}");
        WriteLine($".GetAwaiter().GetResult() : {gwgrTime.ToShortString()}");
    }

    // Private methods

    private async Task<TimeSpan> MeasureDelay(TimeSpan delay)
    {
        var now = CpuTimestamp.Now;
        await Task.Delay(delay).ConfigureAwait(false);
        return now.Elapsed;
    }
}
