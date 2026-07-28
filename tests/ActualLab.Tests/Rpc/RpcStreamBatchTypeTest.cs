using System.Reflection;
using System.Runtime.CompilerServices;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Serialization;
using MessagePack;

namespace ActualLab.Tests.Rpc;

/// <summary>
/// A type outside the item hierarchy of the streams used here. It counts its own
/// construction, so a test can tell "rejected" from "built, then cast-failed".
/// </summary>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public partial class ForeignBatchItem
{
    public static int ConstructionCount;

    [DataMember, MemoryPackOrder(0), Key(0)]
    public int Value { get; set; }

    public ForeignBatchItem()
        => Interlocked.Increment(ref ConstructionCount);
}

[Trait("Category", "Rpc")]
public class RpcStreamBatchTypeTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void IsPolymorphicSeesThroughArrays()
    {
        // The array carries the polymorphism of its elements - this is what lets the batch slot
        // stay T[] instead of being widened to object to reach the polymorphic path.
        RpcArgumentSerializer.IsPolymorphic(typeof(ITuple)).Should().BeTrue();
        RpcArgumentSerializer.IsPolymorphic(typeof(ITuple[])).Should().BeTrue();
        RpcArgumentSerializer.IsPolymorphic(typeof(ITuple[][])).Should().BeTrue();

        RpcArgumentSerializer.IsPolymorphic(typeof(int)).Should().BeFalse();
        RpcArgumentSerializer.IsPolymorphic(typeof(int[])).Should().BeFalse();
        RpcArgumentSerializer.IsPolymorphic(typeof(ForeignBatchItem[])).Should().BeFalse();

        // [RpcSerializable] types handle their own polymorphism, so neither they nor their
        // arrays get ActualLab's type marker.
        RpcArgumentSerializer.IsPolymorphic(typeof(NonPolymorphicBase)).Should().BeFalse();
        RpcArgumentSerializer.IsPolymorphic(typeof(NonPolymorphicBase[])).Should().BeFalse();
    }

    [Fact]
    public void BatchArgumentsKeepTheExactArrayType()
    {
        var arguments = NewBatchArguments<ITuple>();
        arguments.Type.ItemTypes[1].Should().Be(typeof(ITuple[]));
    }

    [Theory]
    [InlineData("mempack6")]
    [InlineData("msgpack6")]
    [InlineData("json5")]
    [InlineData("njson5")]
    public void ForeignItemTypeIsRejectedBeforeConstruction(string formatKey)
    {
        var serializer = GetArgumentSerializer(formatKey);
        var data = Serialize(serializer, ArgumentList.New(0L, (object)new[] { new ForeignBatchItem { Value = 1 } }));

        // Control: the payload is well-formed and constructible - an object-typed slot builds it,
        // which is exactly what the T[] bound must prevent.
        var constructionCount = Volatile.Read(ref ForeignBatchItem.ConstructionCount);
        Deserialize(serializer, ArgumentList.New<long, object>(0L, null!), data)
            .Should().BeOfType<ForeignBatchItem[]>();
        Volatile.Read(ref ForeignBatchItem.ConstructionCount).Should().Be(constructionCount + 1);

        constructionCount = Volatile.Read(ref ForeignBatchItem.ConstructionCount);
        var deserialize = () => Deserialize(serializer, NewBatchArguments<ITuple>(), data);
        deserialize.Should().Throw<Exception>();
        Volatile.Read(ref ForeignBatchItem.ConstructionCount).Should().Be(constructionCount);
    }

    [Theory]
    [InlineData("mempack6")]
    [InlineData("msgpack6")]
    [InlineData("json5")]
    [InlineData("njson5")]
    public void CollectionOfTheItemTypeIsRejectedToo(string formatKey)
    {
        // IReadOnlyList<out T> is covariant, so List<Tuple<int>> satisfies IReadOnlyList<ITuple>
        // while not being an ITuple[] - no sender can produce it, so the bound must reject it
        // rather than build it and fail the later (T[])items cast.
        var serializer = GetArgumentSerializer(formatKey);
        var data = Serialize(serializer,
            ArgumentList.New(0L, (object)new List<Tuple<int>> { new(1) }));

        var deserialize = () => Deserialize(serializer, NewBatchArguments<ITuple>(), data);
        deserialize.Should().Throw<Exception>();
    }

    [Theory]
    [InlineData("mempack6")]
    [InlineData("msgpack6")]
    [InlineData("json5")]
    [InlineData("njson5")]
    public void DerivedItemArrayStillRoundTrips(string formatKey)
    {
        // Array covariance is the legitimate case: RpcSharedStream.Batcher emits U[] for a
        // homogeneous run, e.g. Tuple<int>[] for an RpcStream<ITuple>.
        var serializer = GetArgumentSerializer(formatKey);
        var items = new[] { new Tuple<int>(1), new Tuple<int>(2) };
        var data = Serialize(serializer, ArgumentList.New(0L, (object)items));

        var result = Deserialize(serializer, NewBatchArguments<ITuple>(), data);
        result.Should().BeOfType<Tuple<int>[]>();
        ((ITuple[])result!).Should().HaveCount(2);
    }

    // Private methods

    private static ArgumentList NewBatchArguments<T>()
    {
        var method = typeof(RpcStream).GetMethod("CreateStreamBatchArguments",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (ArgumentList)method.Invoke(new RpcStream<T>(), [])!;
    }

    private static RpcArgumentSerializer GetArgumentSerializer(string formatKey)
        => RpcSerializationFormat.All
            .Single(x => string.Equals(x.Key, formatKey, StringComparison.Ordinal))
            .ArgumentSerializer;

    private static ReadOnlyMemory<byte> Serialize(RpcArgumentSerializer serializer, ArgumentList arguments)
    {
        using var buffer = new ArrayPoolBuffer<byte>(256);
        serializer.Serialize(arguments, needsPolymorphism: true, buffer);
        return buffer.WrittenMemory.ToArray();
    }

    private static object? Deserialize(
        RpcArgumentSerializer serializer, ArgumentList arguments, ReadOnlyMemory<byte> data)
    {
        serializer.Deserialize(ref arguments, needsPolymorphism: true, data);
        return arguments.GetUntyped(1);
    }
}
