using System.Buffers;

namespace ActualLab.Compression;

/// <summary>
/// The <see cref="ByteEncoder"/> of <see cref="SelectingByteCodec"/>: writes the codec index byte,
/// then lets the selected encoder write right after it, so the frame is never copied.
/// </summary>
public sealed class SelectingByteEncoder : ByteEncoder
{
    private readonly ByteEncoder[] _encoders;

    public SelectingByteCodec Codec { get; }

    public SelectingByteEncoder(SelectingByteCodec codec)
    {
        Codec = codec;
        _encoders = codec.Codecs.Select(x => x.NewEncoder()).ToArray();
    }

    public override void Dispose()
    {
        foreach (var encoder in _encoders)
            encoder.Dispose();
    }

    public override void Encode(ReadOnlySpan<byte> source, IBufferWriter<byte> target)
    {
        if (source.IsEmpty)
            return;

        var index = Codec.Selector.Invoke(source);
        if ((uint)index >= (uint)_encoders.Length)
            throw new InvalidOperationException(
                $"The selector returned codec index {index}, but there are {_encoders.Length} codecs.");

        target.GetSpan(1)[0] = (byte)index;
        target.Advance(1);
        _encoders[index].Encode(source, target);
    }

    public override int GetMaxEncodedLength(int sourceLength)
    {
        if (sourceLength <= 0)
            return 0;

        var maxLength = 0L;
        foreach (var encoder in _encoders)
            maxLength = Math.Max(maxLength, encoder.GetMaxEncodedLength(sourceLength));
        maxLength++; // The codec index byte
        return maxLength >= int.MaxValue ? int.MaxValue : (int)maxLength;
    }

    public override void Reset()
    {
        foreach (var encoder in _encoders)
            encoder.Reset();
    }
}
