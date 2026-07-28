using System.Buffers.Binary;
using ActualLab.Rpc.Serialization.Internal;

namespace ActualLab.Tests.Rpc;

[CollectionDefinition(nameof(TypeSerializerCollection), DisableParallelization = true)]
public class TypeSerializerCollection;

[Collection(nameof(TypeSerializerCollection))]
public class TypeSerializerTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void ByteCachedTypeSurvivesBufferReuse()
    {
        ResetCaches();
        var marker = ByteTypeSerializer.ToBytes(typeof(string)).Bytes.ToArray();
        var buffer = marker.ToArray();
        ReadByteItemType(buffer).Should().Be(typeof(string));
        ByteTypeSerializer.FromBytesCacheSize.Should().Be(1);

        buffer.AsSpan().Fill(0xAB); // That's what the transport does when it recycles the frame buffer

        ReadByteItemType(marker.ToArray()).Should().Be(typeof(string));
        ByteTypeSerializer.FromBytesCacheSize.Should().Be(1);
    }

    [Fact]
    public void TextCachedTypeSurvivesBufferReuse()
    {
        ResetCaches();
        var marker = TextTypeSerializer.ToBytes(typeof(string)).Bytes.ToArray();
        var buffer = marker.ToArray();
        ReadTextItemType(buffer).Should().Be(typeof(string));
        TextTypeSerializer.FromBytesCacheSize.Should().Be(1);

        buffer.AsSpan().Fill(0xAB);

        ReadTextItemType(marker.ToArray()).Should().Be(typeof(string));
        TextTypeSerializer.FromBytesCacheSize.Should().Be(1);
    }

    [Fact]
    public void ByteForgedNameHashIsRejected()
    {
        ResetCaches();
        var marker = ByteTypeSerializer.ToBytes(typeof(string)).Bytes.ToArray();
        ReadByteItemType(marker).Should().Be(typeof(string));
        ByteTypeSerializer.FromBytesCacheSize.Should().Be(1);

        var validHash = BinaryPrimitives.ReadUInt16LittleEndian(marker.AsSpan(2));
        for (var hash = 0; hash < 1000; hash++) {
            if (hash == validHash)
                continue;

            var forgedMarker = marker.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(forgedMarker.AsSpan(2), (ushort)hash);
            Assert.Throws<SerializationException>(() => ReadByteItemType(forgedMarker));
        }
        ByteTypeSerializer.FromBytesCacheSize.Should().Be(1);
    }

    [Fact]
    public void TextMalformedMarkerIsRejected()
    {
        ResetCaches();
        var marker = TextTypeSerializer.ToBytes(typeof(string)).Bytes.ToArray();
        var truncatedMarker = marker[..^1];
        Assert.Throws<SerializationException>(() => ReadTextItemType(truncatedMarker));
        Assert.Throws<SerializationException>(() => TextTypeSerializer.FromBytes(truncatedMarker.AsByteString()));
        TextTypeSerializer.FromBytesCacheSize.Should().Be(0);
    }

    [Fact]
    public async Task ByteCacheRespectsCapacity()
    {
        ByteTypeSerializer.FromBytesCacheCapacity = 8;
        try {
            foreach (var type in GetManyTypes(50))
                ReadByteItemType(ByteTypeSerializer.ToBytes(type).Bytes.ToArray()).Should().Be(type);

            // The cache is pruned in the background, so the bound is reached asynchronously
            await TestExt.When(
                () => ByteTypeSerializer.FromBytesCacheSize.Should().BeLessThanOrEqualTo(8),
                TimeSpan.FromSeconds(5));
        }
        finally {
            ResetCaches();
        }
    }

    [Fact]
    public async Task TextCacheRespectsCapacity()
    {
        TextTypeSerializer.FromBytesCacheCapacity = 8;
        try {
            foreach (var type in GetManyTypes(50))
                ReadTextItemType(TextTypeSerializer.ToBytes(type).Bytes.ToArray()).Should().Be(type);

            // The cache is pruned in the background, so the bound is reached asynchronously
            await TestExt.When(
                () => TextTypeSerializer.FromBytesCacheSize.Should().BeLessThanOrEqualTo(8),
                TimeSpan.FromSeconds(5));
        }
        finally {
            ResetCaches();
        }
    }

    // Private methods

    private static void ResetCaches()
    {
        // Assigning the capacity replaces the cache, which is what we want here
        ByteTypeSerializer.FromBytesCacheCapacity = ByteTypeSerializer.DefaultFromBytesCacheCapacity;
        TextTypeSerializer.FromBytesCacheCapacity = TextTypeSerializer.DefaultFromBytesCacheCapacity;
    }

    private static Type? ReadByteItemType(byte[] bytes)
    {
        var data = (ReadOnlyMemory<byte>)bytes;
        return ByteTypeSerializer.ReadItemType(ref data);
    }

    private static Type? ReadTextItemType(byte[] bytes)
    {
        var data = (ReadOnlyMemory<byte>)bytes;
        return TextTypeSerializer.ReadItemType(ref data);
    }

    private static IEnumerable<Type> GetManyTypes(int count)
    {
        var leafTypes = new[] {
            typeof(int), typeof(long), typeof(short), typeof(byte), typeof(string),
            typeof(bool), typeof(char), typeof(float), typeof(double), typeof(decimal),
        };
        var wrapperTypes = new[] {
            typeof(List<>), typeof(HashSet<>), typeof(Queue<>), typeof(Stack<>), typeof(LinkedList<>),
        };
        for (var i = 0; i < count; i++) {
            var leafType = leafTypes[i % leafTypes.Length];
            var wrapperType = wrapperTypes[(i / leafTypes.Length) % wrapperTypes.Length];
            yield return wrapperType.MakeGenericType(leafType);
        }
    }
}
