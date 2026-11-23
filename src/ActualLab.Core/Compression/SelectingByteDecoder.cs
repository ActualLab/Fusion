using System.Buffers;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Compression;

/// <summary>
/// The <see cref="ByteDecoder"/> of <see cref="SelectingByteCodec"/>: reads the codec index byte and
/// passes the rest of the frame (a slice, not a copy) to that codec's decoder.
/// </summary>
public sealed class SelectingByteDecoder : ByteDecoder
{
    private readonly ByteDecoder[] _decoders;

    public SelectingByteCodec Codec { get; }

    public SelectingByteDecoder(SelectingByteCodec codec)
    {
        Codec = codec;
        _decoders = codec.Codecs.Select(x => x.NewDecoder()).ToArray();
    }

    public override void Dispose()
    {
        foreach (var decoder in _decoders)
            decoder.Dispose();
    }

    public override void Decode(ReadOnlyMemory<byte> source, IBufferWriter<byte> target, int maxLength)
    {
        if (source.IsEmpty)
            return;

        var index = source.Span[0];
        if (index >= _decoders.Length)
            throw Errors.UnknownCodecIndex(index, _decoders.Length);
        if (source.Length == 1) // The encoder never produces an index byte alone
            throw Errors.UnknownCodecIndex(index, _decoders.Length);

        _decoders[index].Decode(source[1..], target, maxLength);
    }

    public override void Reset()
    {
        foreach (var decoder in _decoders)
            decoder.Reset();
    }
}
