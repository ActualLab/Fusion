using Stl.Locking;
using Xunit;
using Xunit.Abstractions;

namespace Stl.Tests.Async
{
    [Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
    public class AsyncLockTest : AsyncLockTestBase
    {
        public AsyncLockTest(ITestOutputHelper @out) : base(@out) { }

        protected override IAsyncLock CreateAsyncLock(ReentryMode reentryMode)
            => new AsyncLock(reentryMode);

        protected override void AssertResourcesReleased()
        { }
    }
}
