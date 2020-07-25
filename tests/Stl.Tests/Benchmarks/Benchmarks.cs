using System;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using Stl.Testing;
using Stl.Time;
using Stl.Time.Internal;
using Xunit;
using Xunit.Abstractions;

namespace Stl.Tests.Benchmarks
{
    [Collection(nameof(PerformanceTests)), Trait("Category", nameof(PerformanceTests))]
    public class BenchmarkTest : TestBase
    {
        public BenchmarkTest(ITestOutputHelper @out) : base(@out) { }

        void RunOne<T>(string title, int opCount, Func<int, T> action)
        {
            action.Invoke(Math.Min(1, opCount / 10));
            var sw = Stopwatch.StartNew();
            var _ = action.Invoke(opCount);
            sw.Stop();
            var rate = opCount / sw.Elapsed.TotalSeconds;
            Out.WriteLine($"{title} ({opCount}): {rate:N3} ops/s");
        }

        void RunAll(int baseOpCount)
        {
            RunOne("Read ManagedThreadId", baseOpCount, opCount => {
                var sum = 0L;
                for (; opCount > 0; opCount--) {
                    sum += Thread.CurrentThread.ManagedThreadId;
                }
                return sum;
            });
            RunOne("Read CoarseStopwatch.ElapsedTicks", baseOpCount, opCount => {
                var sum = 0L;
                for (; opCount > 0; opCount--) {
                    sum += CoarseStopwatch.ElapsedTicks;
                }
                return sum;
            });
            RunOne("Read CoarseStopwatch.NowEpochOffsetTicks", baseOpCount, opCount => {
                var sum = 0L;
                for (; opCount > 0; opCount--) {
                    sum += CoarseStopwatch.NowEpochOffsetTicks;
                }
                return sum;
            });
            RunOne("Read CoarseCpuClock.Now.EpochOffsetTicks", baseOpCount, opCount => {
                var sum = 0L;
                for (; opCount > 0; opCount--) {
                    sum += CoarseCpuClock.Now.EpochOffsetTicks;
                }
                return sum;
            });
            RunOne("Read Environment.TickCount64", baseOpCount, opCount => {
                var sum = 0L;
                for (; opCount > 0; opCount--) {
                    sum += Environment.TickCount64;
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
}
