namespace ActualLab.Tests.Async;

public class AsyncEnumerableExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task SkipNullItemsTest()
    {
        var seq1 = await new object?[] {"", null, "x"}.ToAsyncEnumerable().SkipNullItems().ToListAsync();
        seq1.Should().Equal(["", "x"]);

        var seq2 = await new int?[] {0, null, 1}.ToAsyncEnumerable().SkipNullItems().ToListAsync();
        seq2.Should().Equal([0, 1]);
    }

    [Fact]
    public async Task SkipSyncItemsTest()
    {
        var result = await TestSeq(10).SkipSyncItems().ToListAsync();
        result.Should().Equal([9]);
        result = await TestSeq(10).SkipSyncItems(true).ToListAsync();
        result.Should().Equal([0, 9]);
        result = await TestSeq(10, 4).SkipSyncItems().ToListAsync();

        result.Should().Equal([3, 7, 9]);
        result = await TestSeq(10, 4).SkipSyncItems(true).ToListAsync();
        result.Should().Equal([0, 3, 7, 9]);

        result = await TestSeq(3, 1).SkipSyncItems().ToListAsync();
        result.Should().Equal([0, 1, 2]);
        result = await TestSeq(3, 1).SkipSyncItems(true).ToListAsync();
        result.Should().Equal([0, 1, 2]);

        result = await TestSeq(3, 1, true).SkipSyncItems(true).SuppressExceptions().ToListAsync();
        result.Should().Equal([0, 1, 2]);
    }

    [Fact]
    public async Task SuppressExceptionsTest()
    {
        (await TestSeq(0).SuppressExceptions().ToListAsync()).Should().Equal([]);
        (await TestSeq(0, 1, true).SuppressExceptions().ToListAsync()).Should().Equal([]);
        (await TestSeqCancelled(0, 1).SuppressExceptions().ToListAsync()).Should().Equal([]);

        (await TestSeq(3, 2).SuppressExceptions().ToListAsync()).Should().Equal([0, 1, 2]);
        (await TestSeq(3, 2, true).SuppressExceptions().ToListAsync()).Should().Equal([0, 1, 2]);
        (await TestSeqCancelled(3, 2).SuppressExceptions().ToListAsync()).Should().Equal([0, 1, 2]);
    }

    [Fact]
    public async Task SuppressCancellationTest()
    {
        (await TestSeq(0).SuppressExceptions().ToListAsync()).Should().Equal([]);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestSeq(0, 1, true).SuppressCancellation().ToListAsync().AsTask());
        (await TestSeqCancelled(0, 1).SuppressCancellation().ToListAsync()).Should().Equal([]);

        (await TestSeq(3, 2).SuppressExceptions().ToListAsync()).Should().Equal([0, 1, 2]);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestSeq(3, 2, true).SuppressCancellation().ToListAsync().AsTask());
        (await TestSeqCancelled(3, 2).SuppressCancellation().ToListAsync()).Should().Equal([0, 1, 2]);
    }

    // Private methods

    private async IAsyncEnumerable<int> TestSeq(int count, int mod = int.MaxValue, bool mustFail = false)
    {
        for (var i = 0; i < count; i++) {
            if (i != 0 && i % mod == 0)
                await Task.Delay(100);
            yield return i;
        }
        if (mustFail)
            throw new InvalidOperationException();
    }

    private async IAsyncEnumerable<int> TestSeqCancelled(int count, int mod = int.MaxValue)
    {
        for (var i = 0; i < count; i++) {
            if (i != 0 && i % mod == 0)
                await Task.Delay(100);
            yield return i;
        }
        throw new TaskCanceledException();
    }
}
