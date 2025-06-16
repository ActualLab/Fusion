using System.Text;
using Cysharp.Text;

namespace ActualLab.Tests.Text;

public class StringBuilderExtTest(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private static readonly int[] DefaultVariants = [16, 64, 256];
    private static readonly int DefaultIterationCount = 1_000_000;

    [Fact]
    public void BasicTest()
    {
        for (var i = 0; i < 50; i++)
            DiveIn(i);
        return;

        void DiveIn(int count)
        {
            if (count <= 0)
                return;

            var sb = StringBuilderExt.Acquire();
            sb.Append(count);
            DiveIn(count - 1);
            sb.ToStringAndRelease().Should().Be(count.ToString());
        }
    }

    [Fact]
    public async Task PerformanceTest()
    {
        var options = DefaultVariants;
        var iterationCount = DefaultIterationCount;
        await Benchmark("new StringBuilder(length)", iterationCount, static (c, n) => {
            StringBuilder s = new();
            for (var i = 0; i < n; i++)
                s = new StringBuilder(c);
            s.ToString();
        }, options);
        await Benchmark("StringBuilderExt.Acquire(length) + Release", iterationCount, static (c, n) => {
            StringBuilder s = new();
            for (var i = 0; i < n; i++) {
                s = StringBuilderExt.Acquire(c);
                s.Release();
            }
            s.ToString();
        }, options);
        await Benchmark("ZString.CreateStringBuilder() + Release", iterationCount, static (c, n) => {
            for (var i = 0; i < n; i++) {
                using var s = ZString.CreateStringBuilder();
            }
        }, options);
    }

}
