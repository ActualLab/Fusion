using ActualLab.Locking;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests;

public class ComputeMethodResultPusherTest(ITestOutputHelper @out) : TestBase(@out)
{
    private static readonly Func<string, CancellationToken, Task<int>> NoopCaller
        = (_, _) => Task.FromResult(0);

    [Fact]
    public async Task PushPullRoundtrip()
    {
        var pusher = new ComputeMethodResultPusher<string, int>(NoopCaller);

        using var r = await pusher.LockAndReserve("a");
        r.Key.Should().Be("a");
        r.IsStashed.Should().BeFalse();
        pusher.Reservations.Count.Should().Be(1);

        pusher.TryPull("a", out _).Should().BeFalse();

        // NoopCaller doesn't pull, so state remains Pushing after Push completes
        await r.Push(42);
        r.IsStashed.Should().BeTrue();

        pusher.TryPull("a", out var v1).Should().BeTrue();
        v1.Should().Be(42);

        // TryPull flips state back to Empty but keeps the slot until dispose
        r.IsStashed.Should().BeFalse();
        pusher.Reservations.Count.Should().Be(1);
        pusher.TryPull("a", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CallerConsumesPushedValue()
    {
        ComputeMethodResultPusher<string, int>? pusher = null;
        var pulled = -1;
        pusher = new ComputeMethodResultPusher<string, int>(
            (key, _) => {
                if (pusher!.TryPull(key, out var v))
                    pulled = v;
                return Task.FromResult(pulled);
            });

        using var r = await pusher.LockAndReserve("a");
        await r.Push(42);
        pulled.Should().Be(42);
        // Caller pulled during Push, so the slot is back to Empty
        r.IsStashed.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeRemovesSlotAndReleasesLock()
    {
        var pusher = new ComputeMethodResultPusher<string, int>(NoopCaller);

        var r = await pusher.LockAndReserve("k");
        pusher.Reservations.Count.Should().Be(1);
        r.Dispose();
        pusher.Reservations.Count.Should().Be(0);

        r.Dispose();
        pusher.Reservations.Count.Should().Be(0);

        using var r2 = await pusher.LockAndReserve("k").AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        r2.Key.Should().Be("k");
    }

    [Fact]
    public async Task DisposedReservationDoesNotRemoveFreshOne()
    {
        var pusher = new ComputeMethodResultPusher<string, int>(NoopCaller);

        var r1 = await pusher.LockAndReserve("k");
        await r1.Push(7);
        pusher.TryPull("k", out var v).Should().BeTrue();
        v.Should().Be(7);
        r1.Dispose();

        using var r2 = await pusher.LockAndReserve("k");
        pusher.Reservations.Count.Should().Be(1);
        r1.Dispose(); // Second dispose on stale r1 must not touch r2's slot
        pusher.Reservations.Count.Should().Be(1);
    }

    [Fact]
    public async Task PushAfterDisposeThrows()
    {
        var pusher = new ComputeMethodResultPusher<string, int>(NoopCaller);

        var r = await pusher.LockAndReserve("a");
        r.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await r.Push(1));
    }

    [Fact]
    public async Task PullMissingKeyReturnsFalse()
    {
        var pusher = new ComputeMethodResultPusher<string, int>(NoopCaller);
        pusher.TryPull("missing", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PerKeyLockSerializesReserves()
    {
        var pusher = new ComputeMethodResultPusher<string, int>(NoopCaller, LockReentryMode.Unchecked);
        var r1 = await pusher.LockAndReserve("k");

        var second = pusher.LockAndReserve("k").AsTask();
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
        var pusher = new ComputeMethodResultPusher<string, int>(NoopCaller);
        using var r1 = await pusher.LockAndReserve("a");
        using var r2 = await pusher.LockAndReserve("b").AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        pusher.Reservations.Count.Should().Be(2);
    }

    [Fact]
    public async Task SharedLockSetCtor()
    {
        var locks = new AsyncLockSet<string>(LockReentryMode.Unchecked);
        var pusher = new ComputeMethodResultPusher<string, int>(NoopCaller, locks);
        pusher.Locks.Should().BeSameAs(locks);

        using var r = await pusher.LockAndReserve("a");
        await r.Push(5);
        pusher.TryPull("a", out var v).Should().BeTrue();
        v.Should().Be(5);
    }
}
