using ActualLab.Reflection;

namespace ActualLab.Tests.Reflection;

public class DelegateExtTest
{
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
}
