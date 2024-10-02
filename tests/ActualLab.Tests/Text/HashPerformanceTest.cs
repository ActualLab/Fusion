using CommunityToolkit.HighPerformance;

namespace ActualLab.Tests.Text;

public sealed class HashPerformanceTest(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private static readonly int[] DefaultVariants = [0, 1, 16, 256, 16384];
    private static readonly int DefaultByteCount = 16384 * 1024;

    [Fact]
    public async Task DjbVsXxHash3()
    {
        var byteCount = DefaultByteCount;
        var source = new byte[16384];
        new Random().NextBytes(source);

        await Benchmark("new byte(length).AsSpan().GetDjb2HashCode()", byteCount, (length, n) => {
            var s = source.AsSpan(0, length);
            n /= Math.Max(1, length);
            for (var i = 0; i < n; i++)
                _ = s.GetDjb2HashCode();
        }, DefaultVariants);

        await Benchmark("new byte(length).AsSpan().GetXxHash3L()", byteCount, (length, n) => {
            var s = source.AsSpan(0, length);
            n /= Math.Max(1, length);
            for (var i = 0; i < n; i++)
                _ = s.GetXxHash3L();
        }, DefaultVariants);
    }
}
