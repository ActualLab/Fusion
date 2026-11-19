using ActualLab.Reflection;

namespace ActualLab.Tests.Reflection;

public class DelegateExtTest
{
    private readonly int _field = 1;

    [Fact]
    public void GetInvocationListTest()
    {
        Action<int>? handlers = null;

        DelegateExt.GetInvocationList(handlers).Should().BeEmpty();

        var values = new List<int>();
        Action<int> firstHandler = values.Add;
        Action<int> secondHandler = value => values.Add(value * 2);
        handlers = firstHandler + secondHandler;

        var invocationList = DelegateExt.GetInvocationList(handlers);
        invocationList.Should().Equal(firstHandler, secondHandler);
        foreach (var handler in invocationList)
            handler.Invoke(3);
        values.Should().Equal(3, 6);
    }

    [Fact]
    public void IsCaptureFreeTest()
    {
        var local = 2;
        Func<int, int> staticLambda = static x => x + 1;
        Func<int, int> nonCapturingLambda = x => x + 1;
        Func<int, int> staticMethodGroup = StaticAddOne;
        Func<int, int> instanceMethodGroup = InstanceAddOne;
        Func<int, int> thisCapturingLambda = x => x + _field;
        Func<int, int> localCapturingLambda = x => x + local;
        Func<int, int> thisAndLocalCapturingLambda = x => x + local + _field;

        staticLambda.IsCaptureFree().Should().BeTrue();
        nonCapturingLambda.IsCaptureFree().Should().BeTrue();
        staticMethodGroup.IsCaptureFree().Should().BeTrue();
        instanceMethodGroup.IsCaptureFree().Should().BeFalse();
        thisCapturingLambda.IsCaptureFree().Should().BeFalse();
        localCapturingLambda.IsCaptureFree().Should().BeFalse();
        thisAndLocalCapturingLambda.IsCaptureFree().Should().BeFalse();
    }

    private static int StaticAddOne(int x) => x + 1;
    private int InstanceAddOne(int x) => x + _field;
}
