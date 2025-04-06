namespace ActualLab.Tests.Collections;

public class MutablePropertyBagTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void StringTest()
    {
        var o = new MutablePropertyBag();
        o = o.PassThroughAllSerializers();
        o.Count.Should().Be(0);

        o.KeylessSet("A");
        o = o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<string>().Should().Be("A");
            x.KeylessGet("").Should().Be("A");
            x.Count.Should().Be(1);
        });

        o.KeylessSet("B");
        o = o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<string>().Should().Be("B");
            x.KeylessGet("").Should().Be("B");
            x.Count.Should().Be(1);
        });

        var s = new Symbol("S");
        o.KeylessSet(s);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<string>().Should().Be("B");
            x.KeylessGet("").Should().Be("B");
            x.KeylessGet<Symbol>().Should().Be(s);
            x.Count.Should().Be(2);
        });

        o.KeylessRemove<Symbol>();
        o.KeylessRemove<string>();
        o = o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<string>().Should().BeNull();
            x.KeylessGet("").Should().Be("");
            x.Count.Should().Be(0);
        });

        o.KeylessSet("C");
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

        o.KeylessSet(0L);
        o.KeylessSet(1L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<long>().Should().Be(1L);
            x.KeylessGet(-1L).Should().Be(1L);
            x.Count.Should().Be(1);
        });

        o.KeylessSet(2L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<long>().Should().Be(2L);
            x.KeylessGet(-1L).Should().Be(2L);
            x.Count.Should().Be(1);
        });

        o.KeylessRemove<long>();
        o = o.AssertPassesThroughAllSerializers(x => {
            x.KeylessGet<long>().Should().Be(0L);
            x.KeylessGet(-1L).Should().Be(-1L);
            x.Count.Should().Be(0);
        });

        o.KeylessRemove<long>();
        o.Count.Should().Be(0);

        o.KeylessGet<long>().Should().Be(0L);
        o.KeylessGet<long?>().Should().Be(null);
        o.KeylessSet(1L);
        o.KeylessSet((long?)2L);
        o.Count.Should().Be(2);
        o.KeylessGet<long>().Should().Be(1L);
        o.KeylessGet<long?>().Should().Be(2L);
        o.KeylessSet((long?)null);
        o.Count.Should().Be(1);
        o.KeylessGet<long>().Should().Be(1L);
        o.KeylessGet<long?>().Should().Be(null);
        o.KeylessRemove<long>();
        o.Count.Should().Be(0);

        o.KeylessSet(3L);
        o.Count.Should().Be(1);

        o.Clear();
        o.Count.Should().Be(0);
    }

    [Fact]
    public void SetManyTest()
    {
        var options = new MutablePropertyBag();
        options.KeylessSet(1L);
        options.KeylessSet("A");
        var copy = new MutablePropertyBag();
        copy.SetMany(options.Snapshot);

        copy.Count.Should().Be(2);
        copy.KeylessGet<long>().Should().Be(1L);
        copy.KeylessGet<string>().Should().Be("A");
    }
}
