using ActualLab.Collections.Internal;
using MessagePack;

namespace ActualLab.Serialization;

/// <summary>
/// Defines whether data is stored as raw bytes or as text.
/// </summary>
public enum DataFormat
{
    Bytes = 0,
    Text = 1,
}

/// <summary>
/// A discriminated union of text or binary data, backed by a single byte buffer.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[DataContract, MessagePackObject]
[method: SerializationConstructor]
public readonly partial record struct TextOrBytes(
    [property: DataMember(Order = 0), Key(0)]
    DataFormat Format,
    [property: JsonIgnore, DataMember(Order = 1), Key(1)]
    ReadOnlyMemory<byte> Data
) {
    public static readonly TextOrBytes EmptyBytes = new(DataFormat.Bytes, default!);
    public static readonly TextOrBytes EmptyText = new(DataFormat.Text, default!);

    // Computed properties
    [IgnoreMember]
    public byte[] Bytes => field ?? Data.TryGetUnderlyingArray() ?? Data.ToArray();

    [JsonIgnore, IgnoreDataMember, IgnoreMember]
    public bool IsEmpty => Data.Length == 0;

    public TextOrBytes(string text)
        : this(text.AsMemory()) { }
    public TextOrBytes(byte[] bytes)
        : this(DataFormat.Bytes, bytes.AsMemory())
        => Bytes = bytes;
    public TextOrBytes(ReadOnlyMemory<char> text)
        : this(DataFormat.Text, MemoryCast.Cast<char, byte>(text)) { }
    public TextOrBytes(ReadOnlyMemory<byte> bytes)
        : this(DataFormat.Bytes, bytes) { }

    [JsonConstructor]
    public TextOrBytes(DataFormat format, byte[] bytes)
        : this(format, bytes.AsMemory()) { }

    public override string ToString()
        => ToString(64);
    public string ToString(int maxLength)
    {
        if (Format == DataFormat.Text) {
            var text = MemoryMarshal.Cast<byte, char>(Data.Span);
            var textLength = text.Length;
#if NET5_0_OR_GREATER
            var sText = new string(text[..Math.Min(textLength, maxLength)]);
#else
            var sText = new string(text[..Math.Min(textLength, maxLength)].ToArray());
#endif
            return $"[ {textLength} char(s): `{sText}{(textLength <= maxLength ? "`" : "`...")} ]";
        }

#if NET5_0_OR_GREATER
        var sData = Convert.ToHexString(Data.Span[..Math.Min(Data.Length, maxLength)]);
#else
        var sData = BitConverter.ToString(Data.Span[..Math.Min(Data.Length, maxLength)].ToArray());
#endif
        return $"[ {Data.Length} byte(s): {sData}{(Data.Length <= maxLength ? "" : "...")} ]";
    }

    public static implicit operator TextOrBytes(ReadOnlyMemory<byte> bytes) => new(bytes);
    public static implicit operator TextOrBytes(ReadOnlyMemory<char> text) => new(text);
    public static implicit operator TextOrBytes(byte[] bytes) => new(bytes);
    public static implicit operator TextOrBytes(string text) => new(text);

    public bool IsText(out ReadOnlyMemory<char> text)
    {
        if (Format == DataFormat.Text) {
            text = MemoryCast.Cast<byte, char>(Data);
            return true;
        }

        text = default;
        return false;
    }

    public bool IsBytes(out ReadOnlyMemory<byte> bytes)
    {
        if (Format == DataFormat.Bytes) {
            bytes = Data;
            return true;
        }

        bytes = default;
        return false;
    }

    public TextOrBytes CopyData()
        => new(Format, Data.ToArray());

    // Structural equality

    public int GetDataHashCode()
        => Data.Span.GetXxHash3();

    public bool DataEquals(TextOrBytes other)
        => Data.Span.SequenceEqual(other.Data.Span);
}
