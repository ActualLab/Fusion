using ActualLab.Fusion.Blazor;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Tests.Blazor;

public class SafeDispatcherTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void IsUnsafeShouldRecognizeBlazorsOwnDispatcher()
        => SafeDispatcher.IsUnsafe(Dispatcher.CreateDefault().GetType()).Should().BeTrue();

    [Fact]
    public void IsUnsafeShouldNotRecognizeOtherDispatchers()
        => SafeDispatcher.IsUnsafe(typeof(TestDispatcher)).Should().BeFalse();

    [Fact]
    public void WrapIfUnsafeShouldWrapOnlyUnsafeDispatchers()
    {
        WithSafeDispatcherEnabled(() => {
            SafeDispatcher.WrapIfUnsafe(Dispatcher.CreateDefault()).Should().BeOfType<SafeDispatcher>();

            var testDispatcher = new TestDispatcher();
            SafeDispatcher.WrapIfUnsafe(testDispatcher).Should().BeSameAs(testDispatcher);
        });
    }

    [Fact]
    public async Task CheckAccessShouldBeDelegatedToTheWrappedDispatcher()
    {
        var dispatcher = Dispatcher.CreateDefault();
        var safeDispatcher = WithSafeDispatcherEnabled(() => SafeDispatcher.WrapIfUnsafe(dispatcher));
        safeDispatcher.Should().BeOfType<SafeDispatcher>();

        safeDispatcher.CheckAccess().Should().BeFalse();
        await dispatcher.InvokeAsync(() => safeDispatcher.CheckAccess().Should().BeTrue()).WaitAsync(Timeout);
    }

    // If this one fails, https://github.com/dotnet/aspnetcore/issues/69323 is fixed
    // and SafeDispatcher is no longer needed.
    [Fact]
    public void BlazorsOwnDispatcherShouldStillLeakTheRendererContext()
        => LeaksRendererContext(Dispatcher.CreateDefault()).Should().BeTrue();

    [Fact]
    public void SafeDispatcherShouldNotLeakTheRendererContext()
        => WithSafeDispatcherEnabled(()
            => LeaksRendererContext(SafeDispatcher.WrapIfUnsafe(Dispatcher.CreateDefault())).Should().BeFalse());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryOverloadShouldRunItsWorkItem(bool mustSuppressFlow)
    {
        var dispatcher = WithSafeDispatcherEnabled(() => SafeDispatcher.WrapIfUnsafe(Dispatcher.CreateDefault()));
        var callCount = 0;
        var hasAccessInside = true;

        void Body()
        {
            hasAccessInside &= dispatcher.CheckAccess();
            Interlocked.Increment(ref callCount);
        }

        // The work items are queued inside the suppressed-flow block, but awaited outside of it:
        // an AsyncFlowControl must be disposed on the thread that created it.
        Task actionTask, asyncActionTask;
        Task<int> funcTask, asyncFuncTask;
        using (mustSuppressFlow ? ExecutionContext.SuppressFlow() : default) {
            actionTask = dispatcher.InvokeAsync(Body);
            asyncActionTask = dispatcher.InvokeAsync(async () => { Body(); await Task.CompletedTask; });
            funcTask = dispatcher.InvokeAsync(() => { Body(); return 3; });
            asyncFuncTask = dispatcher.InvokeAsync(async () => { Body(); await Task.CompletedTask; return 4; });
        }

        await Task.WhenAll(actionTask, asyncActionTask, funcTask, asyncFuncTask).WaitAsync(Timeout);
        (await funcTask).Should().Be(3);
        (await asyncFuncTask).Should().Be(4);
        callCount.Should().Be(4);
        hasAccessInside.Should().BeTrue();
    }

    // Private methods

    // A pool thread borrows the idle renderer while another thread queues work behind it with the
    // execution context suppressed - the shape ComputedState's update cycle and NotifyStateHasChanged
    // produce together. Returns whether the renderer's SynchronizationContext stayed on the pool thread.
    private static bool LeaksRendererContext(Dispatcher dispatcher)
    {
        using var rendererEntered = new ManualResetEventSlim();
        using var workItemQueued = new ManualResetEventSlim();
        using var callerDone = new ManualResetEventSlim();
        var isLeaked = false;

        ThreadPool.UnsafeQueueUserWorkItem(_ => {
            dispatcher.InvokeAsync(async () => {
                rendererEntered.Set();
                workItemQueued.Wait(Timeout);
                await Task.CompletedTask;
            });
            isLeaked = dispatcher.CheckAccess();
            callerDone.Set();
        }, null);

        rendererEntered.Wait(Timeout).Should().BeTrue();
        using (ExecutionContext.SuppressFlow())
            _ = dispatcher.InvokeAsync(() => { });
        workItemQueued.Set();
        callerDone.Wait(Timeout).Should().BeTrue();
        return isLeaked;
    }

    private static void WithSafeDispatcherEnabled(Action action)
        => WithSafeDispatcherEnabled(() => { action.Invoke(); return 0; });

    private static T WithSafeDispatcherEnabled<T>(Func<T> func)
    {
        var oldIsEnabled = SafeDispatcher.IsEnabled;
        try {
            SafeDispatcher.IsEnabled = true;
            return func.Invoke();
        }
        finally {
            SafeDispatcher.IsEnabled = oldIsEnabled;
        }
    }

    // Nested types

    private sealed class TestDispatcher : Dispatcher
    {
        public override bool CheckAccess()
            => true;

        public override Task InvokeAsync(Action workItem)
        {
            workItem.Invoke();
            return Task.CompletedTask;
        }

        public override Task InvokeAsync(Func<Task> workItem)
            => workItem.Invoke();

        public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem)
            => Task.FromResult(workItem.Invoke());

        public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem)
            => workItem.Invoke();
    }
}
