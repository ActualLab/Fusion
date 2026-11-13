namespace ActualLab.Tests.Async;

public class TaskErrorObservationTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task SilentAwaitShouldObserveFaultedTask()
    {
        // act
        var isObserved = await IsErrorObserved(async error => {
            await Task.FromException(error).SilentAwait(false);
        });

        // assert
        isObserved.Should().BeTrue();
    }

    [Fact]
    public async Task SilentAwaitShouldObserveTaskFaultingLater()
    {
        // act
        var isObserved = await IsErrorObserved(async error => {
            var source = TaskCompletionSourceExt.New();
            _ = Task.Delay(50).ContinueWith(
                _ => source.TrySetException(error),
                CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            await source.Task.SilentAwait(false);
        });

        // assert
        isObserved.Should().BeTrue();
    }

    [Fact]
    public async Task SilentAwaitShouldObserveFaultedValueTask()
    {
        // act
        var isObserved = await IsErrorObserved(async error => {
            await new ValueTask(Task.FromException(error)).SilentAwait(false);
        });
        var isObservedForGeneric = await IsErrorObserved(async error => {
            await new ValueTask<int>(Task.FromException<int>(error)).SilentAwait(false);
        });

        // assert
        isObserved.Should().BeTrue();
        isObservedForGeneric.Should().BeTrue();
    }

    [Fact]
    public async Task SuppressExceptionsShouldObserveFaultedTask()
    {
        // act
        var isObserved = await IsErrorObserved(async error => {
            await Task.FromException(error).SuppressExceptions().ConfigureAwait(false);
        });
        var isObservedForGeneric = await IsErrorObserved(async error => {
            await Task.FromException<int>(error).SuppressExceptions().ConfigureAwait(false);
        });

        // assert
        isObserved.Should().BeTrue();
        isObservedForGeneric.Should().BeTrue();
    }

    // Private methods

    // UnobservedTaskException is process-global and raised by a finalizer, so this method matches
    // its own error instances, and waits for a deliberately unobserved one to know the GC got there
    private static async Task<bool> IsErrorObserved(Func<Exception, Task> scenario)
    {
        var error = new InvalidOperationException("Error");
        var controlError = new InvalidOperationException("Control");
        var isErrorReported = 0;
        var isControlErrorReported = 0;
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, e) => {
            var errors = e.Exception.InnerExceptions;
            if (errors.Contains(error))
                Volatile.Write(ref isErrorReported, 1);
            if (errors.Contains(controlError))
                Volatile.Write(ref isControlErrorReported, 1);
        };

        TaskScheduler.UnobservedTaskException += handler;
        try {
            await scenario.Invoke(error).ConfigureAwait(false);
            LeaveUnobserved(controlError);
            for (var i = 0; i < 100 && Volatile.Read(ref isControlErrorReported) == 0; i++) {
                await Task.Delay(20).ConfigureAwait(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            Volatile.Read(ref isControlErrorReported).Should().Be(1,
                "an unobserved faulted task must be reported once it's collected");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return Volatile.Read(ref isErrorReported) == 0;
        }
        finally {
            TaskScheduler.UnobservedTaskException -= handler;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LeaveUnobserved(Exception error)
        => _ = Task.FromException(error);
}
