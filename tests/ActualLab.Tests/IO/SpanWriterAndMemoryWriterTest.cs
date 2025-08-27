using ActualLab.IO.Internal;

namespace ActualLab.Tests.IO;

public class SpanWriterAndMemoryWriterTest(ITestOutputHelper @out) : TestBase(@out)
{
    [Fact]
    public void UIntTest()
    {
        var memory = new Memory<byte>(new byte[64]);
        var span = new byte[64];
        foreach (var value in UIntValues()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteUInt(value);
            writer.Offset.Should().Be(4);

            span.WriteUnchecked(value);
            span.ToArray().Should().Equal(memory.ToArray());

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadUInt();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Offset);

            var spanReadValue = span.ReadUnchecked<uint>();
            spanReadValue.Should().Be(value);
        }
    }

    [Fact]
    public void ULongTest()
    {
        var memory = new Memory<byte>(new byte[64]);
        var span = new byte[64];
        foreach (var value in ULongValues()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteULong(value);
            writer.Offset.Should().Be(8);

            span.WriteUnchecked(value);
            span.ToArray().Should().Equal(memory.ToArray());

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadULong();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Offset);

            var spanReadValue = span.ReadUnchecked<ulong>();
            spanReadValue.Should().Be(value);
        }
    }

    [Fact]
    public void VarUIntTest()
    {
        var memory = new Memory<byte>(new byte[64]);
        var span = new byte[64];
        foreach (var value in UIntValues()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteVarUInt(value);
            writer.Offset.Should().BeGreaterThanOrEqualTo(1);
            writer.Offset.Should().BeLessThanOrEqualTo(5);

            span.WriteVarUInt(value);
            span.ToArray().Should().Equal(memory.ToArray());

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadVarUInt();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Offset);

            var (spanReadValue, offset) = span.ReadVarUInt();
            spanReadValue.Should().Be(value);
            offset.Should().Be(writer.Offset);
        }
    }

    [Fact]
    public void VarULongTest()
    {
        var memory = new Memory<byte>(new byte[64]);
        var span = new byte[64];
        foreach (var value in ULongValues()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteVarULong(value);
            writer.Offset.Should().BeGreaterThanOrEqualTo(1);
            writer.Offset.Should().BeLessThanOrEqualTo(10);

            span.WriteVarULong(value);
            span.ToArray().Should().Equal(memory.ToArray());

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadVarULong();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Offset);

            var (spanReadValue, offset) = span.ReadVarULong();
            spanReadValue.Should().Be(value);
            offset.Should().Be(writer.Offset);
        }
    }

    [Fact]
    public void L1ULongTest()
    {
        var memory = new Memory<byte>(new byte[64]);
        foreach (var value in ULongValues()) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteAltVarULong(value);
            writer.Offset.Should().BeGreaterThanOrEqualTo(1);
            writer.Offset.Should().BeLessThanOrEqualTo(10);

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadAltVarULong();
            readValue.Should().Be(value);
            reader.Offset.Should().Be(writer.Offset);
        }
    }

    [Fact]
    public void LVarSpanTest()
    {
        var memory = new Memory<byte>(new byte[600]);
        foreach (var bytes in ByteSequences(260)) {
            var writer = new SpanWriter(memory.Span);
            writer.WriteLVarSpan(bytes);
            writer.Offset.Should().BeGreaterThanOrEqualTo(1);
            writer.Offset.Should().BeLessThanOrEqualTo(bytes.Length + 5);

            var reader = new MemoryReader(memory);
            var readValue = reader.ReadLVarSpan();
            readValue.Length.Should().Be(bytes.Length);
            readValue.ToArray().Should().Equal(bytes);
            reader.Offset.Should().Be(writer.Offset);
        }
    }

    private static IEnumerable<byte[]> ByteSequences(int count)
    {
        for (int i = 0; i < count; i++)
            yield return Enumerable.Range(0, i).Select(x => (byte)x).ToArray();
    }

    private static IEnumerable<ulong> ULongValues()
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

    private static IEnumerable<uint> UIntValues()
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
