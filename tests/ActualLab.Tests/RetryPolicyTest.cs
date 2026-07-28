using ActualLab.Resilience;

namespace ActualLab.Tests;

public class RetryPolicyTest(ITestOutputHelper @out) : TestBase(@out)
{
    private const int SpinTryCount = 1_000_000;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task SuperTransientErrorBacksOffTest()
    {
        var policy = new RetryPolicy(RetryDelaySeq.Fixed(RetryDelay, 0));
        var tryCount = 0;
        var startedAt = CpuTimestamp.Now;
        var result = await policy.Apply(_ => {
            if (++tryCount < 4)
                throw new RetryRequiredException();

            return Task.FromResult(tryCount);
        });
        var elapsed = startedAt.Elapsed;
        Out.WriteLine($"tryCount: {tryCount}, elapsed: {elapsed.ToShortString()}");

        result.Should().Be(4);
        elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task ZeroDelayRetryIsCancellableTest()
    {
        var policy = new RetryPolicy(RetryDelaySeq.Zero);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var tryCount = 0;
        var applyTask = policy.Apply<Unit>(_ => {
            if (Interlocked.Increment(ref tryCount) > SpinTryCount)
                throw new TerminalException("RetryPolicy.Apply spins: it never observes its CancellationToken.");

            throw new RetryRequiredException();
        }, cts.Token);
        await applyTask.SilentAwait();
        Out.WriteLine($"tryCount: {tryCount}");

        applyTask.IsCanceled.Should().BeTrue();
    }
}
