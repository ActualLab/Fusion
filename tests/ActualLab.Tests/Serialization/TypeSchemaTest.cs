using System.Runtime.Serialization;

namespace ActualLab.Tests.Serialization;

public sealed class TypeSchemaTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void PrimitiveOnlyAllowsPrimitives()
    {
        // arrange
        var bag = PropertyBag<TypeSchema.PrimitiveOnly>.Empty
            .Set("name", "test")
            .Set("size", 1234L)
            .Set("width", 42)
            .Set("at", new Moment(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)));

        // act
        var result = PassThroughMessagePack(bag);

        // assert
        result["name"].Should().Be("test");
        result["size"].Should().Be(1234L);
        result["width"].Should().Be(42);
        result["at"].Should().BeOfType<Moment>();
    }

    [Fact]
    public void PrimitiveOnlyRejectsOtherTypesOnSet()
    {
        // arrange
        var bag = PropertyBag<TypeSchema.PrimitiveOnly>.Empty;

        // act
        var set = () => bag.Set("unit", default(Unit));

        // assert
        set.Should().Throw<SerializationException>();
    }

    [Fact]
    public void PrimitiveOnlyRejectsDisallowedTypeOnDeserialization()
    {
        // arrange - the schema has to hold against bytes it didn't produce, since the writer is remote
        var data = Write(PropertyBag.Empty.Set("unit", default(Unit)));

        // act
        var read = () => Read<PropertyBag<TypeSchema.PrimitiveOnly>>(data);

        // assert
        read.Should().Throw<Exception>()
            .Where(e => e is SerializationException || e.InnerException is SerializationException);
    }

    [Fact]
    public void SchemaDoesNotChangeTheWireFormat()
    {
        // arrange
        var anyBag = PropertyBag.Empty.Set("name", "test").Set("size", 1234L);
        var strictBag = PropertyBag<TypeSchema.PrimitiveOnly>.Empty.Set("name", "test").Set("size", 1234L);

        // act
        var anyData = Write(anyBag);
        var strictData = Write(strictBag);

        // assert
        strictData.Should().Equal(anyData);
    }

    [Fact]
    public void AnyAllowsEverything()
    {
        // arrange
        var bag = PropertyBag.Empty.Set("unit", default(Unit));

        // act
        var result = PassThroughMessagePack(bag);

        // assert
        result["unit"].Should().Be(default(Unit));
    }

    // Private methods

    private static byte[] Write<T>(T value)
    {
        using var buffer = MessagePackByteSerializer.Default.ToTyped<T>().Write(value);
        return buffer.WrittenMemory.ToArray();
    }

    private static T Read<T>(byte[] data)
        => MessagePackByteSerializer.Default.ToTyped<T>().Read(data, out _);

    private static PropertyBag<TSchema> PassThroughMessagePack<TSchema>(PropertyBag<TSchema> value)
        where TSchema : TypeSchema, new()
        => Read<PropertyBag<TSchema>>(Write(value));
}
