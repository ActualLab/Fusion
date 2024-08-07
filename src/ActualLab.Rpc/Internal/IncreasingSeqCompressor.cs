using Microsoft.Toolkit.HighPerformance.Buffers;

namespace ActualLab.Rpc.Internal;

public static class IncreasingSeqCompressor
{
    public static byte[] Serialize(IEnumerable<long> values)
    {
        var lastValue = 0L;
        var writer = new ArrayPoolBufferWriter<byte>();
        foreach (var value in values) {
            var delta = value - lastValue;
            lastValue = value;
            if (delta < 0)
                throw new ArgumentOutOfRangeException(nameof(values));

            var span = writer.GetSpan(10);
            var size = Bits.Write7BitEncoded((ulong)delta, span);
            writer.Advance(size);
        }
        return writer.WrittenSpan.ToArray();
    }

    public static List<long> Deserialize(byte[] data)
    {
        var result = new List<long>();
        var span = data.AsSpan();
        var lastValue = 0L;
        while (span.Length != 0) {
            var size = Bits.Read7BitEncoded(span, out var delta);
            span = span[size..];
            lastValue += (long)delta;
            result.Add(lastValue);
        }
        return result;
    }
}
