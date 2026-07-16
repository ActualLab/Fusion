using ActualLab.Diagnostics;
using ActualLab.Fusion.Internal;
using ActualLab.Tests;

namespace ActualLab.Fusion.Tests.Internal;

public class InvalidatedHandlerSetTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void Test()
    {
        const int iterationCount = 200;
        for (var iteration = 0; iteration < iterationCount; iteration++)
            for (var size = 0; size < 10; size++)
                RunTest(size, (iteration + 1.0) / iterationCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public async Task InvokeIsolatesHandlerFailures(int size)
    {
        var computed = await Computed.New(_ => Task.FromResult(1)).Update();
        var calledIndexes = new HashSet<int>();
        var actions = Enumerable.Range(0, size)
            .Select<int, Action<Computed>>(index => _ => {
                calledIndexes.Add(index);
                if (index == 0)
                    throw new InvalidOperationException("failure");
            });
        var handlerSet = new InvalidatedHandlerSet(actions);

        handlerSet.Invoke(computed);

        calledIndexes.Should().BeEquivalentTo(Enumerable.Range(0, size));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public async Task InvokeDoesNotAllocate(int size)
    {
        var computed = await Computed.New(_ => Task.FromResult(1)).Update();
        var actions = Enumerable.Range(0, size)
            .Select<int, Action<Computed>>(index => _ => {
                if (index < 0)
                    throw new InvalidOperationException();
            });
        var handlerSet = new InvalidatedHandlerSet(actions);
        handlerSet.Invoke(computed);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
            handlerSet.Invoke(computed);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        allocated.Should().Be(0);
    }

    private void RunTest(int size, double removalProbability)
    {
        var usedIndexes = new HashSet<int>();

        var indexes = Enumerable.Range(0, size).ToList();
        var actions = indexes.Select(CreateAction).ToList();
        var handlerSet = new InvalidatedHandlerSet(actions);

        usedIndexes.Clear();
        handlerSet.Invoke(null!);
        usedIndexes.Count.Should().Be(actions.Count);

        var sampler = Sampler.RandomShared(removalProbability);
        var removedIndexes = new HashSet<int>(
            indexes.Where(_ => sampler.Next()));
        var removedActions = new HashSet<Action<Computed>>(
            actions.Where((_, i) => removedIndexes.Contains(i)));
        actions = actions.Where(a => !removedActions.Contains(a)).ToList();
        foreach (var action in removedActions)
            handlerSet.Remove(action);

        usedIndexes.Clear();
        handlerSet.Invoke(null!);
        usedIndexes.Count.Should().Be(actions.Count);
        if (usedIndexes.Count != 0)
            usedIndexes.Should().AllSatisfy(i => removedIndexes.Contains(i).Should().BeFalse());

        Action<Computed> CreateAction(int index)
            => _ => usedIndexes.Add(index).Should().BeTrue();
    }
}
