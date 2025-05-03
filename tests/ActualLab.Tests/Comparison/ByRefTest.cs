using ActualLab.Comparison;

namespace ActualLab.Tests.Comparison;

public class ByRefTest
{
    public record TestRecord(string X);

    [Fact]
    public void BasicTest()
    {
        var r0 = ByRef.New(default(TestRecord));
        var r1 = ByRef.New(new TestRecord("X"));
        var r2 = ByRef.New(new TestRecord("X"));
        r0.Target.Should().NotBe(r1.Target);
        r1.Target.Should().Be(r2.Target);

        var refs = new[] {r0!, r1, r2};
        for (var i1 = 0; i1 < refs.Length; i1++) {
            r1.GetHashCode();
            for (var i2 = 0; i2 < refs.Length; i2++) {
                if (i1 == i2)
                    refs[i1].Should().Be(refs[i2]);
                else
                    refs[i1].Should().NotBe(refs[i2]);
            }
        }
    }
}
