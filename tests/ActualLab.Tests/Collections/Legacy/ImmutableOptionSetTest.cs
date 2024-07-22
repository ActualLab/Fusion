namespace ActualLab.Tests.Collections.Legacy;

public class ImmutableOptionSetTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void StringTest()
    {
        var o = new ImmutableOptionSet();
        o = o.PassThroughAllSerializers();
        o.Items.Count.Should().Be(0);
        o.Should().Be(o);

        var o1 = o.Set("A");
        o1 = o1.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().Be("A");
            x.GetOrDefault("").Should().Be("A");
            x.Items.Count.Should().Be(1);
            x.Should().Be(x);
            x.Should().NotBe(o);
        });

        var o2 = o1.Set("B");
        o2 = o2.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().Be("B");
            x.GetOrDefault("").Should().Be("B");
            x.Items.Count.Should().Be(1);
            x.Should().Be(x);
            x.Should().NotBe(o1);
            x.Should().NotBe(o);
        });

        var o3 = o2.Remove<string>();
        o3.AssertPassesThroughAllSerializers(x => {
            x.Get<string>().Should().BeNull();
            x.GetOrDefault("").Should().Be("");
            x.Items.Count.Should().Be(0);
            x.Should().Be(o);
            x.Should().NotBe(o1);
            x.Should().NotBe(o2);
        });
    }

    [Fact]
    public void StructTest()
    {
        var options = new ImmutableOptionSet();
        options = options.PassThroughAllSerializers();
        options.Items.Count.Should().Be(0);

        // We use Int64 type here b/c JSON serializer
        // deserializes integers to this type.

        options = options.Set(1L);
        options = options.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(1L);
            o.GetOrDefault(-1L).Should().Be(1L);
            o.Items.Count.Should().Be(1);
        });

        options = options.Set(2L);
        options = options.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(2L);
            o.GetOrDefault(-1L).Should().Be(2L);
            o.Items.Count.Should().Be(1);
        });

        options = options.Remove<long>();
        options.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(0L);
            o.GetOrDefault(-1L).Should().Be(-1L);
            o.Items.Count.Should().Be(0);
        });
    }

    [Fact]
    public void SetManyTest()
    {
        var options = new ImmutableOptionSet().Set(1L).Set("A");
        var copy = new ImmutableOptionSet().SetMany(options);

        copy.Items.Count.Should().Be(2);
        copy.GetOrDefault<long>().Should().Be(1L);
        copy.Get<string>().Should().Be("A");
    }
}
