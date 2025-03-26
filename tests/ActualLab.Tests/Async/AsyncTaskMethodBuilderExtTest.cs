using System.Reflection;

namespace ActualLab.Tests.Async;

public class AsyncTaskMethodBuilderExtTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public async Task NewTest()
    {
        var b = AsyncTaskMethodBuilderExt.New();
        HasRunContinuationsAsynchronouslyStateFlag(b.Task).Should().BeTrue();
        b.SetResult();
        await b.Task;
    }

    [Fact]
    public async Task GenericNewTest()
    {
        var b = AsyncTaskMethodBuilderExt.New<int>();
        HasRunContinuationsAsynchronouslyStateFlag(b.Task).Should().BeTrue();
        b.SetResult(1);
        (await b.Task).Should().Be(1);
    }

    private bool HasRunContinuationsAsynchronouslyStateFlag(Task task)
    {
        var stateFlags = GetStateFlags(task);
        return (stateFlags & (int)TaskContinuationOptions.RunContinuationsAsynchronously) != 0;
    }

    private int GetStateFlags(Task task)
        => (int)task.GetType()
            .GetField("m_stateFlags", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(task)!;
}
