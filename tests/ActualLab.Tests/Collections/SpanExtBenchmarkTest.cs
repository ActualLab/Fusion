using ActualLab.Internal;

namespace ActualLab.Tests.Collections;

public class SpanExtBenchmarkTest(ITestOutputHelper @out) : BenchmarkTestBase(@out)
{
    private static readonly int DefaultIterationCount = 100_000_000;

    [Fact]
    public async Task ReadVarUInt32Test()
    {
        var buffer = new byte[100];
        var remainder = buffer.AsSpan();
        const int valueCount = 10;
        for (var i = 0; i < valueCount; i++) {
            var v = (uint)((1 << i) - 1);
            remainder.WriteVarUInt32(v);
            var (readValue, size) = remainder.ReadVarUInt32();
            readValue.Should().Be(v);
            ReadVarUInt32(remainder).Should().Be((v, size));
            remainder = remainder[size..];
        }

        var iterationCount = DefaultIterationCount;
        await Benchmark("buffer.ReadVarUInt32", iterationCount, n => {
            var span = buffer;
            for (var i = 0; i < n; i++) {
                var offset = 0;
                for (var j = 0; j < valueCount; j++)
                    (_, offset) = span.ReadVarUInt32(offset);
            }
        });
        await Benchmark("ReadVarUInt32", iterationCount, n => {
            var span = buffer;
            for (var i = 0; i < n; i++) {
                var offset = 0;
                for (var j = 0; j < valueCount; j++)
                    (_, offset) = ReadVarUInt32(span, offset);
            }
        });
    }

    public static (uint Value, int Offset) ReadVarUInt32(ReadOnlySpan<byte> span, int offset = 0)
    {
        byte b;
        var value = 0u;
        var shift = 0;

        if (BitConverter.IsLittleEndian) {
            var buffer = span.ReadUnchecked<uint>(offset);
            for (; shift < 28; shift += 7) {
                b = (byte)buffer;
                buffer >>= 8;
                offset++;
                value |= (uint)(b & 0x7f) << shift;
                if (b <= 0x7f)
                    goto exit;
            }
        }
        else {
            for (; shift < 28; shift += 7) {
                b = span[offset++];
                value |= (uint)(b & 0x7f) << shift;
                if (b <= 0x7f)
                    goto exit;
            }
        }

        // Read the 10th byte. Since we already read 63 bits (7 * 9),
        // the value of this byte must fit within 1 bit (64 - 63),
        // and it must not have the high bit set.
        b = span[offset++];
        if (b > 15)
            throw Errors.InvalidVarLengthEncodedValue();

        value |= (uint)b << shift;
    exit:
        return (value, offset);
    }
}
