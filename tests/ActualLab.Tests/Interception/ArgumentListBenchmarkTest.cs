using ActualLab.Interception;

namespace ActualLab.Tests.Interception;

public class ArgumentListBenchmarkTest(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private static readonly int DefaultIterationCount = 10_000_000;

    [Fact]
    public async Task GetLikeMethod0Test()
    {
        var iterationCount = DefaultIterationCount;
        await Benchmark("ArgumentList.New(ct)", iterationCount, static n => {
            var ct = CancellationToken.None;
            for (var i = 0; i < n; i++)
                _ = ArgumentList.New(ct);
        });
        await Benchmark("new [] { ct }", iterationCount, static n => {
            var ct = CancellationToken.None;
            for (var i = 0; i < n; i++)
                _ = new object?[] { ct };
        });
    }

    [Fact]
    public async Task GetLikeMethod1Test()
    {
        var iterationCount = DefaultIterationCount;
        await Benchmark("ArgumentList.New(i, ct)", iterationCount, static n => {
            var ct = CancellationToken.None;
            for (var i = 0; i < n; i++)
                _ = ArgumentList.New(i, ct);
        });
        await Benchmark("new [] { i, ct }", iterationCount, static n => {
            var ct = CancellationToken.None;
            for (var i = 0; i < n; i++)
                _ = new object?[] { i, ct };
        });
    }

    [Fact]
    public async Task GetLikeMethod2Test()
    {
        var iterationCount = DefaultIterationCount;
        await Benchmark("ArgumentList.New(i, i, ct)", iterationCount, static n => {
            var ct = CancellationToken.None;
            for (var i = 0; i < n; i++)
                _ = ArgumentList.New(i, i, ct);
        });
        await Benchmark("new [] { i, i, ct }", iterationCount, static n => {
            var ct = CancellationToken.None;
            for (var i = 0; i < n; i++)
                _ = new object?[] { i, i, ct };
        });
    }

    [Fact]
    public async Task CommandLikeTest()
    {
        var iterationCount = DefaultIterationCount;
        await Benchmark("ArgumentList.New(obj, ct)", iterationCount, static n => {
            var obj = new object();
            var ct = CancellationToken.None;
            for (var i = 0; i < n; i++)
                _ = ArgumentList.New(obj, ct);
        });
        await Benchmark("new [] { obj, ct }", iterationCount, static n => {
            var obj = new object();
            var ct = CancellationToken.None;
            for (var i = 0; i < n; i++)
                _ = new object?[] { obj, ct };
        });
    }

    [Fact]
    public async Task CancellationTokenGetTest()
    {
        var iterationCount = DefaultIterationCount;
        await Benchmark("Get CT in ArgumentList.New(obj, ct)", iterationCount, static n => {
            var obj = new object();
            var ct = CancellationToken.None;
            var l = ArgumentList.New(obj, ct);
            for (var i = 0; i < n; i++)
                ct = l.GetCancellationToken(1);
            Use(ct);
        });
        await Benchmark("Get CT in new [] { obj, ct }", iterationCount, static n => {
            var obj = new object();
            var ct = CancellationToken.None;
            var l = new object?[] { obj, ct };
            for (var i = 0; i < n; i++)
                ct = (CancellationToken)l[1]!;
            Use(ct);
        });
    }

    [Fact]
    public async Task CancellationTokenGetSetTest()
    {
        var iterationCount = DefaultIterationCount;
        await Benchmark("Get + set CT in ArgumentList.New(obj, ct)", iterationCount, static n => {
            var obj = new object();
            var ct = CancellationToken.None;
            var l = ArgumentList.New(obj, ct);
            for (var i = 0; i < n; i++)
                _ = l.GetCancellationToken(1);
            Use(ct);
        });
        await Benchmark("Get + set CT in new [] { obj, ct }", iterationCount, static n => {
            var obj = new object();
            var ct = CancellationToken.None;
            var l = new object?[] { obj, ct };
            for (var i = 0; i < n; i++) {
                ct = (CancellationToken)l[1]!;
                l[1] = ct;
            }
            Use(ct);
        });
    }

    private static void Use(object obj)
        => _ = obj.GetHashCode();
}
