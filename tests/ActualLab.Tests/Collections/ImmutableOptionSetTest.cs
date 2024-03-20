namespace ActualLab.Tests.Collections;

public class ImmutableOptionSetTest
{
    [Fact]
    public void StringTest()
    {
        var o = new ImmutableOptionSet();
        o = o.PassThroughAllSerializers();
        o.Items.Count.Should().Be(0);
        o.Should().Be(o);

        var o1 = o.Set("A");
        o1 = o1.PassThroughAllSerializers();
        o1.Get<string>().Should().Be("A");
        o1.GetOrDefault("").Should().Be("A");
        o1.Items.Count.Should().Be(1);
        o1.Should().Be(o1);
        o1.Should().NotBe(o);

        var o2 = o1.Set("B");
        o2 = o2.PassThroughAllSerializers();
        o2.Get<string>().Should().Be("B");
        o2.GetOrDefault("").Should().Be("B");
        o2.Items.Count.Should().Be(1);
        o2.Should().Be(o2);
        o2.Should().NotBe(o1);
        o2.Should().NotBe(o);

        var o3 = o2.Remove<string>();
        o3 = o3.PassThroughAllSerializers();
        o3.Get<string>().Should().BeNull();
        o3.GetOrDefault("").Should().Be("");
        o3.Items.Count.Should().Be(0);
        o3.Should().Be(o);
        o3.Should().NotBe(o1);
        o3.Should().NotBe(o2);
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
        options = options.PassThroughAllSerializers();
        options.GetOrDefault<long>().Should().Be(1L);
        options.GetOrDefault(-1L).Should().Be(1L);
        options.Items.Count.Should().Be(1);

        options = options.Set(2L);
        options = options.PassThroughAllSerializers();
        options.GetOrDefault<long>().Should().Be(2L);
        options.GetOrDefault(-1L).Should().Be(2L);
        options.Items.Count.Should().Be(1);

        options = options.Remove<long>();
        options = options.PassThroughAllSerializers();
        options.GetOrDefault<long>().Should().Be(0L);
        options.GetOrDefault(-1L).Should().Be(-1L);
        options.Items.Count.Should().Be(0);
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

    [Fact]
    public void ReplaceTest()
    {
        var options = new ImmutableOptionSet();
        options = options.Replace(null, "A");
        options.Get<string>().Should().Be("A");
        options = options.Replace("A", "B");
        options.Get<string>().Should().Be("B");

        options = options.Replace("C", "D");
        options.Get<string>().Should().Be("B");
    }
}
