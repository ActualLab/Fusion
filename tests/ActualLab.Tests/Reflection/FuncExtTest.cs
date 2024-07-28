using ActualLab.Interception;
using ActualLab.Reflection;

namespace ActualLab.Tests.Reflection;

public class FuncExtTest
{
    public const int ArgCount = 12;

    [Fact]
    public void GetActionTest()
    {
        for (var i = 0; i < ArgCount; i++) {
            var tArguments = Enumerable.Range(0, i).Select(_ => typeof(int)).ToArray();
            var tFunc = FuncExt.GetActionType(tArguments);
            tFunc.IsGenericType.Should().Be(i != 0);
            var gArgs = tFunc.GetGenericArguments();
            gArgs.Length.Should().Be(i);
            gArgs.Should().Equal(tArguments);
        }
    }

    [Fact]
    public void GetFuncTest()
    {
        for (var i = 0; i < ArgCount; i++) {
            var tArguments = Enumerable.Range(0, i).Select(_ => typeof(int)).ToArray();
            var tFunc = FuncExt.GetFuncType(tArguments, typeof(object));
            tFunc.IsGenericType.Should().BeTrue();
            var gArgs = tFunc.GetGenericArguments();
            gArgs.Length.Should().Be(i + 1);
            gArgs.Take(i).Should().Equal(tArguments);
            gArgs.Last().Should().Be(typeof(object));
        }
    }
}
