namespace ActualLab.Tests.Collections;

public class PropertyBagTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void StringTest()
    {
        var o = new PropertyBag();
        o = o.PassThroughAllSerializers();
        o.Count.Should().Be(0);

        var o1 = o.KeylessSet("A");
        o1 = o1.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<string>().Should().Be("A");
            x.KeylessGet("").Should().Be("A");
            x.Count.Should().Be(1);
        });

        var o2 = o1.KeylessSet("B");
        o2 = o2.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<string>().Should().Be("B");
            x.KeylessGet("").Should().Be("B");
            x.Count.Should().Be(1);
        });

        var s = new Symbol("S");
        var o3 = o2.KeylessSet(s);
        o3 = o3.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<string>().Should().Be("B");
            x.KeylessGet("").Should().Be("B");
            x.KeylessGet<Symbol>().Should().Be(s);
            x.Count.Should().Be(2);
        });

        var o4 = o3.KeylessRemove<Symbol>().KeylessRemove<string>();
        o4.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<string>().Should().BeNull();
            x.KeylessGet("").Should().Be("");
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

        o = o.KeylessSet(0L);
        o = o.KeylessSet(1L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<long>().Should().Be(1L);
            x.KeylessGet(-1L).Should().Be(1L);
            x.Count.Should().Be(1);
        });

        o = o.KeylessSet(2L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<long>().Should().Be(2L);
            x.KeylessGet(-1L).Should().Be(2L);
            x.Count.Should().Be(1);
        });

        o = o.KeylessRemove<long>();
        o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<long>().Should().Be(0L);
            x.KeylessGet(-1L).Should().Be(-1L);
            x.Count.Should().Be(0);
        });
        o = o.KeylessRemove<long>();
        o.Count.Should().Be(0);

        o.KeylessGet<long>().Should().Be(0L);
        o.KeylessGet<long?>().Should().Be(null);
        o = o.KeylessSet(1L);
        o = o.KeylessSet((long?)2L);
        o.Count.Should().Be(2);
        o.KeylessGet<long>().Should().Be(1L);
        o.KeylessGet<long?>().Should().Be(2L);
        o = o.KeylessSet((long?)null);
        o.Count.Should().Be(1);
        o.KeylessGet<long>().Should().Be(1L);
        o.KeylessGet<long?>().Should().Be(null);

        o = o.KeylessRemove<long>();
        o.Count.Should().Be(0);
    }

    [Fact]
    public void SetManyTest()
    {
        var o = new PropertyBag().KeylessSet(1L).KeylessSet("A");
        var copy = new PropertyBag().SetMany(o);

        copy.Count.Should().Be(2);
        copy.KeylessGet<long>().Should().Be(1L);
        copy.KeylessGet<string>().Should().Be("A");
    }
}
