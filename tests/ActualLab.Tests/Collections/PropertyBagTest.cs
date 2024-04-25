namespace ActualLab.Tests.Collections;

public class PropertyBagTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void StringTest()
    {
        var o = new PropertyBag();
        o = o.PassThroughAllSerializers();
        o.Count.Should().Be(0);

        var o1 = o.Set("A");
        o1 = o1.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().Be("A");
            x.GetOrDefault("").Should().Be("A");
            x.Count.Should().Be(1);
        });

        var o2 = o1.Set("B");
        o2 = o2.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().Be("B");
            x.GetOrDefault("").Should().Be("B");
            x.Count.Should().Be(1);
        });

        var o3 = o2.Remove<string>();
        o3.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().BeNull();
            x.GetOrDefault("").Should().Be("");
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

        o = o.Set(1L);
        o = o.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(1L);
            o.GetOrDefault(-1L).Should().Be(1L);
            o.Count.Should().Be(1);
        });

        o = o.Set(2L);
        o = o.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(2L);
            o.GetOrDefault(-1L).Should().Be(2L);
            o.Count.Should().Be(1);
        });

        o = o.Remove<long>();
        o.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(0L);
            o.GetOrDefault(-1L).Should().Be(-1L);
            o.Count.Should().Be(0);
        });
    }

    [Fact]
    public void SetManyTest()
    {
        var o = new PropertyBag().Set(1L).Set("A");
        var copy = new PropertyBag().SetMany(o);

        copy.Count.Should().Be(2);
        copy.GetOrDefault<long>().Should().Be(1L);
        copy.Get<string>().Should().Be("A");
    }
}
