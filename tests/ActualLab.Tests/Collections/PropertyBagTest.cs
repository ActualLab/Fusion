namespace ActualLab.Tests.Collections;

public class PropertyBagTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void StringTest()
    {
        var o = new PropertyBag();
        o = o.PassThroughAllSerializers();
        o.Count.Should().Be(0);

        var o1 = o.SetKeyless("A");
        o1 = o1.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<string>().Should().Be("A");
            x.GetKeyless("").Should().Be("A");
            x.Count.Should().Be(1);
        });

        var o2 = o1.SetKeyless("B");
        o2 = o2.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<string>().Should().Be("B");
            x.GetKeyless("").Should().Be("B");
            x.Count.Should().Be(1);
        });

        var s = new Symbol("S");
        var o3 = o2.SetKeyless(s);
        o3 = o3.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<string>().Should().Be("B");
            x.GetKeyless("").Should().Be("B");
            x.GetKeyless<Symbol>().Should().Be(s);
            x.Count.Should().Be(2);
        });

        var o4 = o3.RemoveKeyless<Symbol>().RemoveKeyless<string>();
        o4.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<string>().Should().BeNull();
            x.GetKeyless("").Should().Be("");
            x.Count.Should().Be(0);
        });
    }

    [Fact]
    public void StructTest()
    {
        var o = new PropertyBag();
        o = o.PassThroughAllSerializers();
        o.Count.Should().Be(0);

        // We use Int64 type here b/c JSON serializer
        // deserializes integers to this type.

        o = o.SetKeyless(0L);
        o = o.SetKeyless(1L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<long>().Should().Be(1L);
            x.GetKeyless(-1L).Should().Be(1L);
            x.Count.Should().Be(1);
        });

        o = o.SetKeyless(2L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<long>().Should().Be(2L);
            x.GetKeyless(-1L).Should().Be(2L);
            x.Count.Should().Be(1);
        });

        o = o.RemoveKeyless<long>();
        o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<long>().Should().Be(0L);
            x.GetKeyless(-1L).Should().Be(-1L);
            x.Count.Should().Be(0);
        });
        o = o.RemoveKeyless<long>();
        o.Count.Should().Be(0);

        o.GetKeyless<long>().Should().Be(0L);
        o.GetKeyless<long?>().Should().Be(null);
        o = o.SetKeyless(1L);
        o = o.SetKeyless((long?)2L);
        o.Count.Should().Be(2);
        o.GetKeyless<long>().Should().Be(1L);
        o.GetKeyless<long?>().Should().Be(2L);
        o = o.SetKeyless((long?)null);
        o.Count.Should().Be(1);
        o.GetKeyless<long>().Should().Be(1L);
        o.GetKeyless<long?>().Should().Be(null);

        o = o.RemoveKeyless<long>();
        o.Count.Should().Be(0);
    }

    [Fact]
    public void SetManyTest()
    {
        var o = new PropertyBag().SetKeyless(1L).SetKeyless("A");
        var copy = new PropertyBag().SetMany(o);

        copy.Count.Should().Be(2);
        copy.GetKeyless<long>().Should().Be(1L);
        copy.GetKeyless<string>().Should().Be("A");
    }
}
