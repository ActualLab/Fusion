using ActualLab.Fusion.EntityFramework;

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
        o = o.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().Be("A");
            x.GetOrDefault("").Should().Be("A");
            x.Count.Should().Be(1);
        });

        o.Set("B");
        o = o.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().Be("B");
            x.GetOrDefault("").Should().Be("B");
            x.Count.Should().Be(1);
        });

        var s = new DbShard("S");
        o.Set(s);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().Be("B");
            x.GetOrDefault("").Should().Be("B");
            x.GetOrDefault<DbShard>().Should().Be(s);
            x.Count.Should().Be(2);
        });

        o.Remove<string>();
        o.Remove<DbShard>();
        o = o.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().BeNull();
            x.GetOrDefault("").Should().Be("");
            x.Count.Should().Be(0);
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

        o.Set(0L);
        o.Set(1L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetOrDefault<long>().Should().Be(1L);
            x.GetOrDefault(-1L).Should().Be(1L);
            x.Count.Should().Be(1);
        });

        o.Set(2L);
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetOrDefault<long>().Should().Be(2L);
            x.GetOrDefault(-1L).Should().Be(2L);
            x.Count.Should().Be(1);
        });

        o.Remove<long>();
        o = o.AssertPassesThroughAllSerializers(x => {
            x.GetOrDefault<long>().Should().Be(0L);
            x.GetOrDefault(-1L).Should().Be(-1L);
            x.Count.Should().Be(0);
        });

        o.Remove<long>();
        o.Count.Should().Be(0);

        o.GetOrDefault<long>().Should().Be(0L);
        o.GetOrDefault<long?>().Should().Be(null);
        o.Set(1L);
        o.Set((long?)2L);
        o.Count.Should().Be(2);
        o.GetOrDefault<long>().Should().Be(1L);
        o.GetOrDefault<long?>().Should().Be(2L);
        o.Set((long?)null);
        o.Count.Should().Be(1);
        o.GetOrDefault<long>().Should().Be(1L);
        o.GetOrDefault<long?>().Should().Be(null);
        o.Remove<long>();
        o.Count.Should().Be(0);

        o.Set(3L);
        o.Count.Should().Be(1);

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
