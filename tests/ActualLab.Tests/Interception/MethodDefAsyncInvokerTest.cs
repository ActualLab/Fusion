using System.Reflection;
using ActualLab.Interception;

namespace ActualLab.Tests.Interception;

public class MethodDefAsyncInvokerTest
{
    private static readonly MethodInfo Method = typeof(MethodDefAsyncInvokerTest)
        .GetMethod(nameof(Get), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo VoidMethod = typeof(MethodDefAsyncInvokerTest)
        .GetMethod(nameof(GetVoid), BindingFlags.Static | BindingFlags.NonPublic)!;

    [Fact]
    public async Task CompletedTask()
    {
        var invoker = CreateInvoker(static _ => Task.FromResult(1));

        var result = invoker.Invoke(new object());

        result.IsCompletedSuccessfully.Should().BeTrue();
        (await result.ConfigureAwait(false)).Should().Be(1);
    }

    [Fact]
    public async Task IncompleteTask()
    {
        var taskSource = TaskCompletionSourceExt.New<int>();
        var invoker = CreateInvoker(_ => taskSource.Task);

        var result = invoker.Invoke(new object());

        result.IsCompleted.Should().BeFalse();
        taskSource.SetResult(1);
        (await result.ConfigureAwait(false)).Should().Be(1);
    }

    [Fact]
    public async Task FaultedTask()
    {
        var error = new InvalidOperationException();
        var invoker = CreateInvoker(_ => Task.FromException<int>(error));

        var result = invoker.Invoke(new object());
        Func<Task> awaitResult = async () => await result.ConfigureAwait(false);

        var assertion = await awaitResult.Should().ThrowAsync<InvalidOperationException>();
        assertion.Which.Should().BeSameAs(error);
    }

    [Fact]
    public async Task CanceledTask()
    {
        var cancellationToken = new CancellationToken(true);
        var invoker = CreateInvoker(_ => Task.FromCanceled<int>(cancellationToken));

        var result = invoker.Invoke(new object());
        Func<Task> awaitResult = async () => await result.ConfigureAwait(false);

        var assertion = await awaitResult.Should().ThrowAsync<OperationCanceledException>();
        assertion.Which.CancellationToken.Should().Be(cancellationToken);
    }

    [Fact]
    public async Task CompletedVoidTask()
    {
        var invoker = CreateVoidInvoker(static _ => Task.CompletedTask);

        var result = invoker.Invoke(new object());

        result.IsCompletedSuccessfully.Should().BeTrue();
        (await result.ConfigureAwait(false)).Should().BeNull();
    }

    [Fact]
    public async Task IncompleteVoidTask()
    {
        var taskSource = TaskCompletionSourceExt.New();
        var invoker = CreateVoidInvoker(_ => taskSource.Task);

        var result = invoker.Invoke(new object());

        result.IsCompleted.Should().BeFalse();
        taskSource.SetResult();
        (await result.ConfigureAwait(false)).Should().BeNull();
    }

    [Fact]
    public async Task FaultedVoidTask()
    {
        var error = new InvalidOperationException();
        var invoker = CreateVoidInvoker(_ => Task.FromException(error));

        var result = invoker.Invoke(new object());
        Func<Task> awaitResult = async () => await result.ConfigureAwait(false);

        var assertion = await awaitResult.Should().ThrowAsync<InvalidOperationException>();
        assertion.Which.Should().BeSameAs(error);
    }

    [Fact]
    public async Task CanceledVoidTask()
    {
        var cancellationToken = new CancellationToken(true);
        var invoker = CreateVoidInvoker(_ => Task.FromCanceled(cancellationToken));

        var result = invoker.Invoke(new object());
        Func<Task> awaitResult = async () => await result.ConfigureAwait(false);

        var assertion = await awaitResult.Should().ThrowAsync<OperationCanceledException>();
        assertion.Which.CancellationToken.Should().Be(cancellationToken);
    }

    private static Func<object, ValueTask<object?>> CreateInvoker(Func<ArgumentList, Task<int>> intercepted)
    {
        var invoker = new MethodDef(typeof(MethodDefAsyncInvokerTest), Method).InterceptedObjectAsyncInvoker;
        return proxy => invoker.Invoke(new Invocation(proxy, Method, ArgumentList.New(), intercepted));
    }

    private static Func<object, ValueTask<object?>> CreateVoidInvoker(Func<ArgumentList, Task> intercepted)
    {
        var invoker = new MethodDef(typeof(MethodDefAsyncInvokerTest), VoidMethod).InterceptedObjectAsyncInvoker;
        return proxy => invoker.Invoke(new Invocation(proxy, VoidMethod, ArgumentList.New(), intercepted));
    }

    private static Task<int> Get()
        => Task.FromResult(1);

    private static Task GetVoid()
        => Task.CompletedTask;
}
