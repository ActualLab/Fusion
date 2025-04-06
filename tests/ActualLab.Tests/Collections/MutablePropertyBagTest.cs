namespace ActualLab.Tests.Collections;

public class MutablePropertyBagTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void StringTest()
    {
        var o = new MutablePropertyBag();
        o = o.PassThroughAllSerializers();
        o.Count.Should().Be(0);

        o.SetKeyless("A");
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<string>().Should().Be("A");
            x.GetKeyless("").Should().Be("A");
            x.Count.Should().Be(1);
        });

        o.SetKeyless("B");
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<string>().Should().Be("B");
            x.GetKeyless("").Should().Be("B");
            x.Count.Should().Be(1);
        });

        var s = new Symbol("S");
        o.SetKeyless(s);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<string>().Should().Be("B");
            x.GetKeyless("").Should().Be("B");
            x.GetKeyless<Symbol>().Should().Be(s);
            x.Count.Should().Be(2);
        });

        o.RemoveKeyless<Symbol>();
        o.RemoveKeyless<string>();
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<string>().Should().BeNull();
            x.GetKeyless("").Should().Be("");
            x.Count.Should().Be(0);
        });

        o.SetKeyless("C");
        o.Clear();
        o.Count.Should().Be(0);
    }

    [Fact]
    public void StructTest()
    {
        var o = new MutablePropertyBag();
        o = o.PassThroughAllSerializers();
        o.Count.Should().Be(0);

        // We use Int64 type here b/c JSON serializer
        // deserializes integers to this type.

        o.SetKeyless(0L);
        o.SetKeyless(1L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<long>().Should().Be(1L);
            x.GetKeyless(-1L).Should().Be(1L);
            x.Count.Should().Be(1);
        });

        o.SetKeyless(2L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<long>().Should().Be(2L);
            x.GetKeyless(-1L).Should().Be(2L);
            x.Count.Should().Be(1);
        });

        o.RemoveKeyless<long>();
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetKeyless<long>().Should().Be(0L);
            x.GetKeyless(-1L).Should().Be(-1L);
            x.Count.Should().Be(0);
        });

        o.RemoveKeyless<long>();
        o.Count.Should().Be(0);

        o.GetKeyless<long>().Should().Be(0L);
        o.GetKeyless<long?>().Should().Be(null);
        o.SetKeyless(1L);
        o.SetKeyless((long?)2L);
        o.Count.Should().Be(2);
        o.GetKeyless<long>().Should().Be(1L);
        o.GetKeyless<long?>().Should().Be(2L);
        o.SetKeyless((long?)null);
        o.Count.Should().Be(1);
        o.GetKeyless<long>().Should().Be(1L);
        o.GetKeyless<long?>().Should().Be(null);
        o.RemoveKeyless<long>();
        o.Count.Should().Be(0);

        o.SetKeyless(3L);
        o.Count.Should().Be(1);

        o.Clear();
        o.Count.Should().Be(0);
    }

    [Fact]
    public void SetManyTest()
    {
        var options = new MutablePropertyBag();
        options.SetKeyless(1L);
        options.SetKeyless("A");
        var copy = new MutablePropertyBag();
        copy.SetMany(options.Snapshot);

        copy.Count.Should().Be(2);
        copy.GetKeyless<long>().Should().Be(1L);
        copy.GetKeyless<string>().Should().Be("A");
    }
}
