using ActualLab.Testing.Collections;
using ActualLab.Time.Internal;

namespace ActualLab.Tests.Benchmarks;

[Collection(nameof(PerformanceTests)), Trait("Category", nameof(PerformanceTests))]
public class BenchmarkTest(ITestOutputHelper @out) : TestBase(@out)
{
    private void RunOne<T>(string title, int opCount, Func<int, T> action)
    {
        action(Math.Min(1, opCount / 10));
        var sw = Stopwatch.StartNew();
        _ = action(opCount);
        sw.Stop();
        var rate = opCount / sw.Elapsed.TotalSeconds;
        Out.WriteLine($"{title} ({opCount}): {rate:N3} ops/s");
    }

    private void RunAll(int baseOpCount)
    {
        RunOne("Read ManagedThreadId", baseOpCount, opCount => {
            var sum = 0L;
            for (; opCount > 0; opCount--) {
                sum += Environment.CurrentManagedThreadId;
            }
            return sum;
        });
        RunOne("Read CoarseClockHelper.ElapsedTicks", baseOpCount, opCount => {
            var sum = 0L;
            for (; opCount > 0; opCount--) {
                sum += CoarseClockHelper.ElapsedTicks;
            }
            return sum;
        });
        RunOne("Read CoarseClockHelper.NowEpochOffsetTicks", baseOpCount, opCount => {
            var sum = 0L;
            for (; opCount > 0; opCount--) {
                sum += CoarseClockHelper.NowEpochOffsetTicks;
            }
            return sum;
        });
        RunOne("Read CoarseCpuClock.Now.EpochOffsetTicks", baseOpCount, opCount => {
            var sum = 0L;
            var clock = CoarseCpuClock.Instance;
            for (; opCount > 0; opCount--) {
                sum += clock.Now.EpochOffsetTicks;
            }
            return sum;
        });
        RunOne("Read Environment.TickCount64", baseOpCount, opCount => {
            var sum = 0L;
            for (; opCount > 0; opCount--) {
#if NETFRAMEWORK
                sum += Environment.TickCount;
#else
                sum += Environment.TickCount64;
#endif
            }
            return sum;
        });
        RunOne("Read DateTime.Now.Ticks", baseOpCount, opCount => {
            var sum = 0L;
            for (; opCount > 0; opCount--) {
                sum += DateTime.Now.Ticks;
            }
            return sum;
        });
    }

    // [Fact]
    [Fact(Skip = "Performance")]
    public void RunBenchmarks()
    {
        RunAll(1_000_000);
        Out.WriteLine("");
        Thread.Sleep(1000);

        RunAll(10_000_000);
        Out.WriteLine("");
        Thread.Sleep(1000);
    }
}
