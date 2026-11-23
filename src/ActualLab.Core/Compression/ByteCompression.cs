namespace ActualLab.Compression;

/// <summary>
/// A compression codec: creates its <see cref="ByteCompressor"/>s and <see cref="ByteDecompressor"/>s,
/// and the preset dictionaries they start from. A dictionary is an opaque object only this codec
/// understands; it's prepared once, never changes, and is meant to be shared by all compressors.
/// </summary>
public abstract class ByteCompression
{
    public abstract ByteCompressor NewCompressor(object? dictionary = null);
    public abstract ByteDecompressor NewDecompressor(object? dictionary = null);

    // Builds a dictionary from sample data, e.g. typical frames; for some codecs that's just LoadDictionary
    public virtual object CreateDictionary(ReadOnlySpan<byte> data)
        => LoadDictionary(data);

    public abstract object LoadDictionary(ReadOnlySpan<byte> data);
    // The inverse of LoadDictionary: the blob it loads the same dictionary from
    public abstract byte[] SaveDictionary(object dictionary);
}
