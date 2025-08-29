using ActualLab.Internal;
using ActualLab.IO.Internal;

namespace ActualLab.Tests.IO;

public class SpanWriterAndMemoryWriterTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void UInt32Test()
    {
        var memory = new Memory<byte>(new byte[64]);
        var span = new byte[64];
        foreach (var value in UInt32Values()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteUInt32(value);
            writer.Position.Should().Be(4);

            span.WriteUnchecked(value);
            span.ToArray().Should().Equal(memory.ToArray());

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadUInt32();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Position);

            var spanReadValue = span.ReadUnchecked<uint>();
            spanReadValue.Should().Be(value);
        }
    }

    [Fact]
    public void UInt64Test()
    {
        var memory = new Memory<byte>(new byte[64]);
        var span = new byte[64];
        foreach (var value in UInt64Values()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteUInt64(value);
            writer.Position.Should().Be(8);

            span.WriteUnchecked(value);
            span.ToArray().Should().Equal(memory.ToArray());

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadUInt64();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Position);

            var spanReadValue = span.ReadUnchecked<ulong>();
            spanReadValue.Should().Be(value);
        }
    }

    [Fact]
    public void VarUInt32Test()
    {
        var memory = new Memory<byte>(new byte[64]);
        var span = new byte[64];
        foreach (var value in UInt32Values()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteVarUInt32(value);
            writer.Position.Should().BeGreaterThanOrEqualTo(1);
            writer.Position.Should().BeLessThanOrEqualTo(5);

            span.WriteVarUInt32(value);
            span.ToArray().Should().Equal(memory.ToArray());

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadVarUInt32();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Position);

            var (spanReadValue, offset) = span.ReadVarUInt32();
            spanReadValue.Should().Be(value);
            offset.Should().Be(writer.Position);
        }
    }

    [Fact]
    public void VarUInt64Test()
    {
        var memory = new Memory<byte>(new byte[64]);
        var span = new byte[64];
        foreach (var value in UInt64Values()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteVarUInt64(value);
            writer.Position.Should().BeGreaterThanOrEqualTo(1);
            writer.Position.Should().BeLessThanOrEqualTo(10);

            span.WriteVarUInt64(value);
            span.ToArray().Should().Equal(memory.ToArray());

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadVarUInt64();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Position);

            var (spanReadValue, offset) = span.ReadVarUInt64();
            spanReadValue.Should().Be(value);
            offset.Should().Be(writer.Position);
        }
    }

    [Fact]
    public void AltVarUInt64Test()
    {
        var memory = new Memory<byte>(new byte[64]);
        foreach (var value in UInt64Values()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteAltVarUInt64(value);
            writer.Position.Should().BeGreaterThanOrEqualTo(1);
            writer.Position.Should().BeLessThanOrEqualTo(10);

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadAltVarUInt64();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Position);
        }
    }

    [Fact]
    public void LVarSpanTest()
    {
        var memory = new Memory<byte>(new byte[600]);
        foreach (var bytes in ByteSequences(260)) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteLVarSpan(bytes);
            writer.Position.Should().BeGreaterThanOrEqualTo(1);
            writer.Position.Should().BeLessThanOrEqualTo(bytes.Length + 5);

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadLVarSpan(1024);
            readValue.Length.Should().Be(bytes.Length);
            readValue.ToArray().Should().Equal(bytes);
            reader.Offset.Should().Be(writer.Position);
        }
    }

    private static IEnumerable<byte[]> ByteSequences(int count)
    {
        for (int i = 0; i < count; i++)
            yield return Enumerable.Range(0, i).Select(x => (byte)x).ToArray();
    }

    private static IEnumerable<ulong> UInt64Values()
    {
        yield return 0ul;
        var v = 1ul;
        for (int i = 0; i < 64; i++) {
            yield return v;        // 2^i
            yield return v + 1ul;  // 2^i + 1
            v <<= 1;
        }
        yield return ulong.MaxValue;
    }

    private static IEnumerable<uint> UInt32Values()
    {
        yield return 0u;
        var v = 1u;
        for (int i = 0; i < 32; i++) {
            yield return v;       // 2^i
            yield return v + 1u;  // 2^i + 1
            v <<= 1;
        }
        yield return uint.MaxValue;
    }
}
