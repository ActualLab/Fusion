using ActualLab.IO;
using CommunityToolkit.HighPerformance.Buffers;

namespace ActualLab.Rpc.Internal;

/// <summary>
/// Compresses and decompresses monotonically increasing sequences of <see cref="long"/> values using variable-length encoding.
/// </summary>
public static class IncreasingSeqCompressor
{
    public static byte[] Serialize(IEnumerable<long> values)
    {
        var lastValue = 0L;
        var writer = new ArrayPoolBufferWriter<byte>(ArrayPools.SharedBytePool);
        foreach (var value in values) {
            var delta = value - lastValue;
            lastValue = value;
            if (delta < 0)
                throw new ArgumentOutOfRangeException(nameof(values));

            var span = writer.GetSpan(10);
            var size = span.WriteVarUInt64((ulong)delta);
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
            var (delta, offset) = span.ReadVarUInt64();
            span = span[offset..];
            lastValue += (long)delta;
            result.Add(lastValue);
        }
        return result;
    }
}
