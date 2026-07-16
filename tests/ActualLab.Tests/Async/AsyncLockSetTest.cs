using System.Reflection;
using ActualLab.Locking;
using ActualLab.Generators;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Async;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class AsyncLockSetTest(ITestOutputHelper @out) : AsyncLockTestBase(@out)
{
    protected AsyncLockSet<string> CheckedFailSet { get; } = new(LockReentryMode.CheckedFail);
    protected AsyncLockSet<string> CheckedPassSet { get; } = new(LockReentryMode.CheckedPass);

    protected sealed class AsyncSetLock<TKey>(AsyncLockSet<TKey> lockSet, TKey key)
        : IAsyncLock<AsyncSetLock<TKey>.Releaser>
        where TKey : notnull
    {
        public AsyncLockSet<TKey> LockSet { get; } = lockSet;
        public TKey Key { get; } = key;

        public LockReentryMode ReentryMode => LockSet.ReentryMode;

        public void Dispose()
        { }

        async ValueTask<IAsyncLockReleaser> IAsyncLock.Lock(CancellationToken cancellationToken)
        {
            var releaser = await LockSet.Lock(Key, cancellationToken).ConfigureAwait(false);
            return new Releaser(releaser);
        }

        public async ValueTask<Releaser> Lock(CancellationToken cancellationToken = default)
        {
            var releaser = await LockSet.Lock(Key, cancellationToken).ConfigureAwait(false);
            return new Releaser(releaser);
        }

        // Nested types

        public class Releaser(AsyncLockSet<TKey>.Releaser releaser) : IAsyncLockReleaser
        {
            private AsyncLockSet<TKey>.Releaser _releaser = releaser;

            public void MarkLockedLocally(bool unmarkOnRelease = true)
                => _releaser.MarkLockedLocally(unmarkOnRelease);

            public void Dispose()
                => _releaser.Dispose();
        }
    }

    protected override IAsyncLock CreateAsyncLock(LockReentryMode reentryMode)
    {
        var key = RandomStringGenerator.Default.Next();
        switch (reentryMode) {
        case LockReentryMode.CheckedFail:
            return new AsyncSetLock<string>(CheckedFailSet, key);
        case LockReentryMode.CheckedPass:
            return new AsyncSetLock<string>(CheckedPassSet, key);
        default:
            throw new ArgumentOutOfRangeException(nameof(reentryMode), reentryMode, null);
        }
    }

    protected override void AssertResourcesReleased()
    {
        void AssertIsEmpty(AsyncLockSet<string> asyncLockSet)
        {
            var fEntries = asyncLockSet.GetType().GetField("_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var entries = fEntries!.GetValue(asyncLockSet);
            entries.Should().NotBeNull();

            var pCount = entries!.GetType().GetProperty("Count");
            var count = (int) pCount!.GetValue(entries)!;
            count.Should().Be(0);
        }

        AssertIsEmpty(CheckedFailSet);
        AssertIsEmpty(CheckedPassSet);
    }

    [Fact]
    public async Task SameKeyReleaseCancellationRaceTest()
    {
        const int IterationCount = 1_000;
        var lockSet = new AsyncLockSet<int>(LockReentryMode.CheckedFail);
        for (var i = 0; i < IterationCount; i++) {
            var owner = await lockSet.Lock(0).ConfigureAwait(false);
            using var cancellationSource = new CancellationTokenSource();
            var cancelledWaiter = lockSet.Lock(0, cancellationSource.Token).AsTask();
            var start = TaskCompletionSourceExt.New();
            var cancelTask = Task.Run(async () => {
                await start.Task.ConfigureAwait(false);
                cancellationSource.Cancel();
            });
            var releaseTask = Task.Run(async () => {
                await start.Task.ConfigureAwait(false);
                owner.Dispose();
            });

            start.SetResult();
            await Task.WhenAll(cancelTask, releaseTask).ConfigureAwait(false);

            try {
                using var cancelledReleaser = await cancelledWaiter.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested) {
                // Cancellation and release intentionally race here.
            }
        }
        lockSet.Count.Should().Be(0);
    }
}
