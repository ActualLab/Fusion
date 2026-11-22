using ActualLab.Interception;
using ActualLab.Interception.Serialization;

namespace ActualLab.Tests.Serialization;

public class ByteSerializerCurrentTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ScopeShouldRestorePreviousSerializer()
    {
        ByteSerializer.Current.Should().BeNull();
        Assert.Throws<InvalidOperationException>(() => ByteSerializer.GetCurrent());
        using (MessagePackByteSerializer.Default.Activate()) {
            ByteSerializer.GetCurrent().Should().BeSameAs(MessagePackByteSerializer.Default);
            using (SystemJsonSerializer.Default.Activate())
                ByteSerializer.Current.Should().BeSameAs(SystemJsonSerializer.Default);
            ByteSerializer.Current.Should().BeSameAs(MessagePackByteSerializer.Default);
        }
        ByteSerializer.Current.Should().BeNull();
    }

    [Fact]
    public void TypedSerializerShouldExposeRootSerializer()
    {
        var serializer = new MessagePackByteSerializer(MessagePackByteSerializer.DefaultOptions);
        var typed = serializer.ToTyped<UniSerialized<int>>();

        var data = typed.Write(UniSerialized.New(5));
        var copy = typed.Read(data.WrittenMemory, out _);

        copy.Value.Should().Be(5);
        ByteSerializer.Current.Should().BeNull();
    }

    [Fact]
    public void DirectlyCreatedTypedSerializerShouldHandleOtherTypes()
    {
        var typed = new MessagePackByteSerializer<int>(MessagePackByteSerializer.DefaultOptions, typeof(int));

        var data = typed.Write("abc", typeof(string));
        var copy = typed.Read(data.WrittenMemory, typeof(string), out _);

        copy.Should().Be("abc");
    }

    [Fact]
    public void ConverterShouldRequireCurrentSerializerOfItsFormat()
    {
        var value = UniSerialized.New(5);

        Assert.Throws<InvalidOperationException>(() => System.Text.Json.JsonSerializer.Serialize(value));
        using (MessagePackByteSerializer.Default.Activate())
            Assert.Throws<InvalidOperationException>(() => System.Text.Json.JsonSerializer.Serialize(value));
    }

    [Fact]
    public void UnscopedMethodsShouldRequireExplicitActivation()
    {
        var serializer = new MessagePackByteSerializer(MessagePackByteSerializer.DefaultOptions);
        var value = UniSerialized.New(5);
        var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, mustClear: false);

        Assert.ThrowsAny<Exception>(() => serializer.WriteUnscoped(buffer, value, typeof(UniSerialized<int>)));
        buffer.Reset();
        using (serializer.Activate()) {
            ByteSerializer.Current.Should().BeSameAs(serializer);
            serializer.WriteUnscoped(buffer, value, typeof(UniSerialized<int>));
            ByteSerializer.Current.Should().BeSameAs(serializer);
            var copy = (UniSerialized<int>)serializer
                .ReadUnscoped(buffer.WrittenMemory, typeof(UniSerialized<int>), out _)!;
            copy.Value.Should().Be(5);
        }
        ByteSerializer.Current.Should().BeNull();
    }

    [Fact]
    public void TypedSerializerShouldActivateRootSerializer()
    {
        var serializer = new MessagePackByteSerializer(MessagePackByteSerializer.DefaultOptions);
        var typed = serializer.ToTyped<UniSerialized<string>>();
        var buffer = new ArrayPoolBuffer<byte>(ArrayPools.SharedBytePool, mustClear: false);

        using (typed.Root.Activate()) {
            ByteSerializer.Current.Should().BeSameAs(serializer);
            typed.WriteUnscoped(buffer, UniSerialized.New("abc"));
            typed.ReadUnscoped(buffer.WrittenMemory, out _).Value.Should().Be("abc");
        }
        ByteSerializer.Current.Should().BeNull();
    }

    [Fact]
    public void JsonConverterShouldHonorNamingPolicyOnRead()
    {
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = new PrefixNamingPolicy() };
        var serializer = new SystemJsonSerializer(options);

        var json = serializer.Write(UniSerialized.New(5), typeof(UniSerialized<int>));
        var copy = (UniSerialized<int>)serializer.Read(json, typeof(UniSerialized<int>))!;

        json.Should().Be("""{"prefix_Json":"5"}""");
        copy.Value.Should().Be(5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArgumentListSerializerShouldScopeUniSerializedItems(bool needsPolymorphism)
    {
        var serializer = new ByteArgumentListSerializer(MessagePackByteSerializer.Default);
        var bag = PropertyBag.Empty.Set("Key", "Value");
        // Only polymorphic lists keep the actual type of an object argument
        var arguments = needsPolymorphism
            ? ArgumentList.New<object>(bag)
            : ArgumentList.New<PropertyBag>(bag);

        var data = serializer.Serialize(arguments, needsPolymorphism);
        var copy = serializer.Deserialize(data, arguments.Type, needsPolymorphism);

        copy.GetUntyped(0).Should().BeOfType<PropertyBag>()
            .Which.Get<string>("Key").Should().Be("Value");
    }

    // Nested types

    private sealed class PrefixNamingPolicy : System.Text.Json.JsonNamingPolicy
    {
        public override string ConvertName(string name) => "prefix_" + name;
    }
}
