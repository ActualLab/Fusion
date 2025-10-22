using ActualLab.Pooling;

namespace ActualLab.Tests.Pooling;

public class WeakReferenceSlimTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void UntypedTest()
    {
        var o1 = new object();
        var o2 = new object();
        var wr = new WeakReferenceSlim(o1);
        wr.Target.Should().Be(o1);
        wr.Target = o2;
        wr.Target.Should().Be(o2);
        wr.Dispose();
        wr.Target.Should().BeNull();
    }

    [Fact]
    public void TypedTest()
    {
        var o1 = new object();
        var o2 = new object();
        var wr = new WeakReferenceSlim<object>(o1);
        wr.Target.Should().Be(o1);
        wr.Target = o2;
        wr.Target.Should().Be(o2);
        wr.Dispose();
        wr.Target.Should().BeNull();
    }
}
