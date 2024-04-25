namespace ActualLab.Tests.Collections;

public class OptionSetTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void StringTest()
    {
        var options = new OptionSet();
        options = options.PassThroughAllSerializers();
        options.Items.Count.Should().Be(0);

        options.Set("A");
        options = options.AssertPassesThroughAllSerializers(o => {
            o.Get<string>().Should().Be("A");
            o.GetOrDefault("").Should().Be("A");
            o.Items.Count.Should().Be(1);
        });

        options.Set("B");
        options = options.AssertPassesThroughAllSerializers(o => {
            o.Get<string>().Should().Be("B");
            o.GetOrDefault("").Should().Be("B");
            o.Items.Count.Should().Be(1);
        });

        options.Remove<string>();
        options = options.AssertPassesThroughAllSerializers(o => {
            o.Get<string>().Should().BeNull();
            o.GetOrDefault("").Should().Be("");
            o.Items.Count.Should().Be(0);
        });

        options.Set("C");
        options.Clear();
        options.Items.Count.Should().Be(0);
    }

    [Fact]
    public void StructTest()
    {
        var options = new OptionSet();
        options = options.PassThroughAllSerializers();
        options.Items.Count.Should().Be(0);

        // We use Int64 type here b/c JSON serializer
        // deserializes integers to this type.

        options.Set(1L);
        options = options.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(1L);
            o.GetOrDefault(-1L).Should().Be(1L);
            o.Items.Count.Should().Be(1);
        });

        options.Set(2L);
        options = options.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(2L);
            o.GetOrDefault(-1L).Should().Be(2L);
            o.Items.Count.Should().Be(1);
        });

        options.Remove<long>();
        options = options.AssertPassesThroughAllSerializers(o => {
            o.GetOrDefault<long>().Should().Be(0L);
            o.GetOrDefault(-1L).Should().Be(-1L);
            o.Items.Count.Should().Be(0);
        });

        options.Set(3L);
        options.Clear();
        options.Items.Count.Should().Be(0);
    }

    [Fact]
    public void SetManyTest()
    {
        var options = new OptionSet();
        options.Set(1L);
        options.Set("A");
        var copy = new OptionSet();
        copy.SetMany(options);

        copy.Items.Count.Should().Be(2);
        copy.GetOrDefault<long>().Should().Be(1L);
        copy.Get<string>().Should().Be("A");
    }
}
