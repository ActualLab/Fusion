namespace ActualLab.Tests.Collections;

public class MutablePropertyBagTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void StringTest()
    {
        var o = new MutablePropertyBag();
        o = o.PassThroughAllSerializers();
        o.Count.Should().Be(0);

        o.Set("A");
        o = o.AssertPassesThroughAllSerializers(o => {
            o.Get<string>().Should().Be("A");
            o.GetOrDefault("").Should().Be("A");
            o.Count.Should().Be(1);
        });

        o.Set("B");
        o = o.AssertPassesThroughAllSerializers(o => {
            o.Get<string>().Should().Be("B");
            o.GetOrDefault("").Should().Be("B");
            o.Count.Should().Be(1);
        });

        o.Remove<string>();
        o = o.AssertPassesThroughAllSerializers(o => {
            o.Get<string>().Should().BeNull();
            o.GetOrDefault("").Should().Be("");
            o.Count.Should().Be(0);
        });

        o.Set("C");
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

        o.Set(1L);
        o = o.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(1L);
            o.GetOrDefault(-1L).Should().Be(1L);
            o.Count.Should().Be(1);
        });

        o.Set(2L);
        o = o.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(2L);
            o.GetOrDefault(-1L).Should().Be(2L);
            o.Count.Should().Be(1);
        });

        o.Remove<long>();
        o = o.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(0L);
            o.GetOrDefault(-1L).Should().Be(-1L);
            o.Count.Should().Be(0);
        });

        o.Set(3L);
        o.Clear();
        o.Count.Should().Be(0);
    }

    [Fact]
    public void SetManyTest()
    {
        var options = new MutablePropertyBag();
        options.Set(1L);
        options.Set("A");
        var copy = new MutablePropertyBag();
        copy.SetMany(options.Snapshot);

        copy.Count.Should().Be(2);
        copy.GetOrDefault<long>().Should().Be(1L);
        copy.Get<string>().Should().Be("A");
    }
}
