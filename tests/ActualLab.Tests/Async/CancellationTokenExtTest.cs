namespace ActualLab.Tests.Async;

public class CancellationTokenExtTest
{
    [Fact]
    public async Task ShouldCancelLinkedAfterDelay()
    {
        using var parent = new CancellationTokenSource();
        using var cts = parent.Token.CreateLinkedTokenSource(TimeSpan.FromMilliseconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(3000, cts.Token));
        cts.IsCancellationRequested.Should().BeTrue();
        parent.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldNotCancelLinkedAfterDelayWhenTimeoutNotPassed()
    {
        using var parent = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
        using var linked = parent.Token.CreateLinkedTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(3000, linked.Token));
        linked.IsCancellationRequested.Should().BeTrue();
        parent.IsCancellationRequested.Should().BeTrue();
    }
}
