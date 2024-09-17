using ActualLab.Collections.Fixed;

namespace ActualLab.Tests.Collections;

public class FixedArrayTest
{
    [Fact]
    public void FixedArray0Test()
    {
        var a0 = FixedArray0<Type>.New();
        a0.Span.Length.Should().Be(0);
        a0.ReadOnlySpan.Length.Should().Be(0);

        var a1 = FixedArray0<Type>.New();
        a1.Should().Be(a0);
        a1.GetHashCode().Should().Be(0);
    }

    [Fact]
    public void FixedArray2Test()
    {
        var a0 = FixedArray2<Type>.New(typeof(int), typeof(string));
        a0.Span.Length.Should().Be(2);
        a0.ReadOnlySpan.Length.Should().Be(2);

        var a1 = FixedArray2<Type>.New(typeof(int), typeof(string));
        a1.Should().Be(a0);
        a1.GetHashCode().Should().Be(a0.GetHashCode());

        var a2 = FixedArray2<Type>.New(typeof(int), typeof(bool));
        a2.Should().NotBe(a0);
        a2.GetHashCode().Should().NotBe(a0.GetHashCode());
    }
}
