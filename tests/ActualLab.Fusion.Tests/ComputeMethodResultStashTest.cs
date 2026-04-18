using ActualLab.Locking;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests;

public class ComputeMethodResultStashTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task StashAndUnstashRoundtrip()
    {
        var stash = new ComputeMethodResultStash<string, int>();

        using (var r = await stash.LockAndReserve("a")) {
            r.Key.Should().Be("a");
            r.IsStashed.Should().BeFalse();
            stash.Count.Should().Be(1);

            stash.TryUnstash("a", out _).Should().BeFalse();
            stash.Count.Should().Be(1);

            r.Stash(42);
            r.IsStashed.Should().BeTrue();

            stash.TryUnstash("a", out var v1).Should().BeTrue();
            v1.Should().Be(42);

            stash.Count.Should().Be(0);
            stash.TryUnstash("a", out _).Should().BeFalse();
        }

        stash.Count.Should().Be(0);
    }

    [Fact]
    public async Task DisposeRemovesSlotAndReleasesLock()
    {
        var stash = new ComputeMethodResultStash<string, int>();

        var r = await stash.LockAndReserve("k");
        stash.Count.Should().Be(1);
        r.Dispose();
        stash.Count.Should().Be(0);

        r.Dispose();
        stash.Count.Should().Be(0);

        using var r2 = await stash.LockAndReserve("k").AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        r2.Key.Should().Be("k");
    }

    [Fact]
    public async Task DisposedReservationDoesNotRemoveFreshOne()
    {
        var stash = new ComputeMethodResultStash<string, int>();

        var r1 = await stash.LockAndReserve("k");
        r1.Stash(7);
        stash.TryUnstash("k", out var v).Should().BeTrue();
        v.Should().Be(7);
        r1.Dispose();

        using var r2 = await stash.LockAndReserve("k");
        stash.Count.Should().Be(1);
        r1.Dispose(); // Second dispose on stale r1 must not touch r2's slot
        stash.Count.Should().Be(1);
    }

    [Fact]
    public async Task StashTwiceThrows()
    {
        var stash = new ComputeMethodResultStash<string, int>();

        using var r = await stash.LockAndReserve("a");
        r.Stash(1);
        Assert.Throws<InvalidOperationException>(() => r.Stash(2));
    }

    [Fact]
    public async Task StashAfterDisposeThrows()
    {
        var stash = new ComputeMethodResultStash<string, int>();

        var r = await stash.LockAndReserve("a");
        r.Dispose();
        Assert.Throws<InvalidOperationException>(() => r.Stash(1));
    }

    [Fact]
    public async Task UnstashMissingKeyReturnsFalse()
    {
        var stash = new ComputeMethodResultStash<string, int>();
        stash.TryUnstash("missing", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PerKeyLockSerializesReserves()
    {
        var stash = new ComputeMethodResultStash<string, int>(LockReentryMode.Unchecked);
        var r1 = await stash.LockAndReserve("k");

        var second = stash.LockAndReserve("k").AsTask();
        await Task.Delay(50);
        second.IsCompleted.Should().BeFalse();

        r1.Dispose();
        var r2 = await second.WaitAsync(TimeSpan.FromSeconds(1));
        r2.Key.Should().Be("k");
        r2.Dispose();
    }

    [Fact]
    public async Task DifferentKeysDoNotBlock()
    {
        var stash = new ComputeMethodResultStash<string, int>();
        using var r1 = await stash.LockAndReserve("a");
        using var r2 = await stash.LockAndReserve("b").AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        stash.Count.Should().Be(2);
    }

    [Fact]
    public async Task SharedLockSetCtor()
    {
        var locks = new AsyncLockSet<string>(LockReentryMode.Unchecked);
        var stash = new ComputeMethodResultStash<string, int>(locks);
        stash.Locks.Should().BeSameAs(locks);

        using var r = await stash.LockAndReserve("a");
        r.Stash(5);
        stash.TryUnstash("a", out var v).Should().BeTrue();
        v.Should().Be(5);
    }
}
