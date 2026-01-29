using ActualLab.Testing.Collections;

namespace ActualLab.Tests.Async;

[Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
public class TaskCompletionHandlerTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task Handler1_CompletedTask()
    {
        var tcs = new TaskCompletionSource<Unit>();
        Task? capturedTask = null;
        object? capturedState = null;
        var state = new object();

        var handler = TaskCompletionHandler.Get(
            tcs.Task,
            state,
            (task, s) => {
                capturedTask = task;
                capturedState = s;
            });
        handler.Attach();

        capturedTask.Should().BeNull(); // Not called yet
        tcs.SetResult(default);
        await Task.Delay(10); // Give time for continuation to run

        capturedTask.Should().BeSameAs(tcs.Task);
        capturedState.Should().BeSameAs(state);
    }

    [Fact]
    public async Task Handler1_AlreadyCompletedTask()
    {
        var completedTask = Task.CompletedTask;
        Task? capturedTask = null;
        object? capturedState = null;
        var state = "test-state";

        var handler = TaskCompletionHandler.Get(
            completedTask,
            state,
            (task, s) => {
                capturedTask = task;
                capturedState = s;
            });
        handler.Attach();

        await Task.Delay(10); // Give time for continuation to run
        capturedTask.Should().BeSameAs(completedTask);
        capturedState.Should().Be(state);
    }

    [Fact]
    public async Task Handler1_FaultedTask()
    {
        var tcs = new TaskCompletionSource<Unit>();
        Task? capturedTask = null;
        object? capturedState = null;
        var state = 42;

        var handler = TaskCompletionHandler.Get(
            tcs.Task,
            state,
            (task, s) => {
                capturedTask = task;
                capturedState = s;
            });
        handler.Attach();

        tcs.SetException(new InvalidOperationException("Test"));
        await Task.Delay(10);

        capturedTask.Should().BeSameAs(tcs.Task);
        capturedTask!.IsFaulted.Should().BeTrue();
        capturedState.Should().Be(state);
    }

    [Fact]
    public async Task Handler1_CancelledTask()
    {
        var tcs = new TaskCompletionSource<Unit>();
        Task? capturedTask = null;
        object? capturedState = null;

        var handler = TaskCompletionHandler.Get(
            tcs.Task,
            null,
            (task, s) => {
                capturedTask = task;
                capturedState = s;
            });
        handler.Attach();

        tcs.SetCanceled();
        await Task.Delay(10);

        capturedTask.Should().BeSameAs(tcs.Task);
        capturedTask!.IsCanceled.Should().BeTrue();
        capturedState.Should().BeNull();
    }

    [Fact]
    public async Task Handler2_CompletedTask()
    {
        var tcs = new TaskCompletionSource<Unit>();
        Task? capturedTask = null;
        object? capturedState1 = null;
        object? capturedState2 = null;
        var state1 = "first";
        var state2 = "second";

        var handler = TaskCompletionHandler.Get(
            tcs.Task,
            state1,
            state2,
            (task, s1, s2) => {
                capturedTask = task;
                capturedState1 = s1;
                capturedState2 = s2;
            });
        handler.Attach();

        tcs.SetResult(default);
        await Task.Delay(10);

        capturedTask.Should().BeSameAs(tcs.Task);
        capturedState1.Should().Be(state1);
        capturedState2.Should().Be(state2);
    }

    [Fact]
    public async Task Handler2_AlreadyCompletedTask()
    {
        var completedTask = Task.CompletedTask;
        Task? capturedTask = null;
        object? capturedState1 = null;
        object? capturedState2 = null;
        var state1 = new object();
        var state2 = 123;

        var handler = TaskCompletionHandler.Get(
            completedTask,
            state1,
            state2,
            (task, s1, s2) => {
                capturedTask = task;
                capturedState1 = s1;
                capturedState2 = s2;
            });
        handler.Attach();

        await Task.Delay(10);
        capturedTask.Should().BeSameAs(completedTask);
        capturedState1.Should().BeSameAs(state1);
        capturedState2.Should().Be(state2);
    }

    [Fact]
    public async Task Handler2_FaultedTask()
    {
        var tcs = new TaskCompletionSource<Unit>();
        Task? capturedTask = null;
        object? capturedState1 = null;
        object? capturedState2 = null;

        var handler = TaskCompletionHandler.Get(
            tcs.Task,
            "a",
            "b",
            (task, s1, s2) => {
                capturedTask = task;
                capturedState1 = s1;
                capturedState2 = s2;
            });
        handler.Attach();

        tcs.SetException(new ArgumentException());
        await Task.Delay(10);

        capturedTask.Should().BeSameAs(tcs.Task);
        capturedTask!.IsFaulted.Should().BeTrue();
        capturedState1.Should().Be("a");
        capturedState2.Should().Be("b");
    }

    [Fact]
    public async Task Handler3_CompletedTask()
    {
        var tcs = new TaskCompletionSource<Unit>();
        Task? capturedTask = null;
        object? capturedState1 = null;
        object? capturedState2 = null;
        object? capturedState3 = null;
        var state1 = "one";
        var state2 = "two";
        var state3 = "three";

        var handler = TaskCompletionHandler.Get(
            tcs.Task,
            state1,
            state2,
            state3,
            (task, s1, s2, s3) => {
                capturedTask = task;
                capturedState1 = s1;
                capturedState2 = s2;
                capturedState3 = s3;
            });
        handler.Attach();

        tcs.SetResult(default);
        await Task.Delay(10);

        capturedTask.Should().BeSameAs(tcs.Task);
        capturedState1.Should().Be(state1);
        capturedState2.Should().Be(state2);
        capturedState3.Should().Be(state3);
    }

    [Fact]
    public async Task Handler3_AlreadyCompletedTask()
    {
        var completedTask = Task.CompletedTask;
        Task? capturedTask = null;
        object? capturedState1 = null;
        object? capturedState2 = null;
        object? capturedState3 = null;

        var handler = TaskCompletionHandler.Get(
            completedTask,
            1,
            2,
            3,
            (task, s1, s2, s3) => {
                capturedTask = task;
                capturedState1 = s1;
                capturedState2 = s2;
                capturedState3 = s3;
            });
        handler.Attach();

        await Task.Delay(10);
        capturedTask.Should().BeSameAs(completedTask);
        capturedState1.Should().Be(1);
        capturedState2.Should().Be(2);
        capturedState3.Should().Be(3);
    }

    [Fact]
    public async Task Handler3_FaultedTask()
    {
        var tcs = new TaskCompletionSource<Unit>();
        Task? capturedTask = null;
        object? capturedState1 = null;
        object? capturedState2 = null;
        object? capturedState3 = null;

        var handler = TaskCompletionHandler.Get(
            tcs.Task,
            "x",
            "y",
            "z",
            (task, s1, s2, s3) => {
                capturedTask = task;
                capturedState1 = s1;
                capturedState2 = s2;
                capturedState3 = s3;
            });
        handler.Attach();

        tcs.SetException(new InvalidOperationException());
        await Task.Delay(10);

        capturedTask.Should().BeSameAs(tcs.Task);
        capturedTask!.IsFaulted.Should().BeTrue();
        capturedState1.Should().Be("x");
        capturedState2.Should().Be("y");
        capturedState3.Should().Be("z");
    }

    [Fact]
    public async Task Handler3_CancelledTask()
    {
        var tcs = new TaskCompletionSource<Unit>();
        Task? capturedTask = null;
        object? capturedState1 = null;
        object? capturedState2 = null;
        object? capturedState3 = null;

        var handler = TaskCompletionHandler.Get(
            tcs.Task,
            null,
            null,
            null,
            (task, s1, s2, s3) => {
                capturedTask = task;
                capturedState1 = s1;
                capturedState2 = s2;
                capturedState3 = s3;
            });
        handler.Attach();

        tcs.SetCanceled();
        await Task.Delay(10);

        capturedTask.Should().BeSameAs(tcs.Task);
        capturedTask!.IsCanceled.Should().BeTrue();
        capturedState1.Should().BeNull();
        capturedState2.Should().BeNull();
        capturedState3.Should().BeNull();
    }

    [Fact]
    public async Task Handler1_WithExecutionContextFlow()
    {
        var tcs = new TaskCompletionSource<Unit>();
        Task? capturedTask = null;
        object? capturedState = null;
        var state = new object();

        var handler = TaskCompletionHandler.Get(
            tcs.Task,
            state,
            (task, s) => {
                capturedTask = task;
                capturedState = s;
            });
        handler.Attach(flowExecutionContext: true);

        tcs.SetResult(default);
        await Task.Delay(10);

        capturedTask.Should().BeSameAs(tcs.Task);
        capturedState.Should().BeSameAs(state);
    }

    [Fact]
    public async Task MultipleHandlers_PoolingWorks()
    {
        // Run multiple handlers sequentially to test pooling
        var callCount = 0;

        for (var i = 0; i < 10; i++) {
            var tcs = new TaskCompletionSource<Unit>();
            var handler = TaskCompletionHandler.Get(
                tcs.Task,
                i,
                (_, s) => Interlocked.Increment(ref callCount));
            handler.Attach();
            tcs.SetResult(default);
            await Task.Delay(5);
        }

        callCount.Should().Be(10);
    }

    [Fact]
    public async Task ConcurrentHandlers()
    {
        var callCount = 0;
        var tasks = new List<TaskCompletionSource<Unit>>();

        // Create multiple handlers concurrently
        for (var i = 0; i < 100; i++) {
            var tcs = new TaskCompletionSource<Unit>();
            tasks.Add(tcs);
            var handler = TaskCompletionHandler.Get(
                tcs.Task,
                i,
                (_, s) => Interlocked.Increment(ref callCount));
            handler.Attach();
        }

        // Complete all tasks
        foreach (var tcs in tasks)
            tcs.SetResult(default);

        await Task.Delay(50);
        callCount.Should().Be(100);
    }
}
