using ActualLab.Locking;
using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Async;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class AsyncLockTest(ITestOutputHelper @out) : AsyncLockTestBase(@out)
{
    [Fact]
    public void InterfaceLockCompletesSynchronouslyTest()
    {
        IAsyncLock asyncLock = new AsyncLock();
        var releaserTask = asyncLock.Lock();

        releaserTask.IsCompletedSuccessfully.Should().BeTrue();
        using var releaser = releaserTask.Result;
    }

    protected override IAsyncLock CreateAsyncLock(LockReentryMode reentryMode)
        => new AsyncLock(reentryMode);

    protected override void AssertResourcesReleased()
    { }
}
