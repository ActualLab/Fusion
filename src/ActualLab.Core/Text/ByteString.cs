using System.ComponentModel;
using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.Text.Internal;
using CommunityToolkit.HighPerformance;
using Cysharp.Text;
using MessagePack;

namespace ActualLab.Text;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable, MessagePackFormatter(typeof(ByteStringMessagePackFormatter))]
[JsonConverter(typeof(ByteStringJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(ByteStringNewtonsoftJsonConverter))]
[TypeConverter(typeof(ByteStringTypeConverter))]
public readonly partial struct ByteString : IEquatable<ByteString>, IComparable<ByteString>, ISerializable
{
    public static readonly byte[] EmptyBytes = [];
    public static ByteString Empty => new(EmptyBytes);

    [DataMember(Order = 0), MemoryPackOrder(0), Key(0)]
    public readonly ReadOnlyMemory<byte> Bytes;

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public int Length {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Bytes.Length;
    }

    [IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public ReadOnlySpan<byte> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Bytes.Span;
    }

    public static ByteString FromBase64(string? base64)
        => base64.IsNullOrEmpty() ? Empty : new(Convert.FromBase64String(base64));
    public static ByteString FromBase64Url(string? base64Url)
        => base64Url.IsNullOrEmpty() ? Empty : new(Base64UrlEncoder.Decode(base64Url).ToArray());

    public static ByteString FromStringAsUtf8(string source, int bufferLength = 1)
        => EncodingExt.Utf8NoBom.GetBytes(source).AsByteString();

    public static ByteString FromStringAsUtf16(string source)
        => source.AsSpan().Cast<char, byte>().ToArray().AsByteString();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ByteString(byte[] bytes)
        => Bytes = bytes;

    [MemoryPackConstructor, SerializationConstructor]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ByteString(ReadOnlyMemory<byte> bytes)
        => Bytes = bytes;

    // Conversion

    public override string ToString()
        => ToString(64);
    public string ToString(int maxLength)
    {
        var length = Bytes.Length;
        return ZString.Concat("[ ",
            length, " byte(s): ", ToHexString(maxLength),
            length <= maxLength ? " ]" : "... ]");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToBase64()
#if !NETSTANDARD2_0
        => Convert.ToBase64String(Bytes.Span);
#else
        => Convert.ToBase64String(Bytes.ToArray());
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToBase64Url()
        => Base64UrlEncoder.Encode(Bytes.Span);

    public string ToHexString(int maxLength = int.MaxValue)
#if NET5_0_OR_GREATER
        => Convert.ToHexString(Bytes.Span[..Math.Min(Bytes.Length, maxLength)]);
#else
        => BitConverter.ToString(Bytes.Span[..Math.Min(Bytes.Length, maxLength)].ToArray());
#endif

    public string ToStringAsUtf8()
#if !NETSTANDARD2_0
        => EncodingExt.Utf8NoBom.GetString(Bytes.Span);
#else
        => EncodingExt.Utf8NoBom.GetDecoder().Convert(Bytes.Span);
#endif

    public string ToStringAsUtf16()
    {
        var span = Bytes.Span;
        return (span.Length & 1) == 0
#if !NETSTANDARD2_0
            ? new string(span.Cast<byte, char>())
#else
            ? span.Cast<byte, char>().ToString()
#endif
            : throw Errors.Format("Invalid sequence length.");
    }

    // Equality

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ByteString other)
        => Bytes.Span.SequenceEqual(other.Bytes.Span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
        => obj is ByteString other && Equals(other);

    public override int GetHashCode()
        => Bytes.Span.GetPartialXxHash3();

    // Comparison

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ByteString other)
        => Bytes.Span.SequenceCompareTo(other.Bytes.Span);

    // Equality & comparison operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(ByteString left, ByteString right) => left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(ByteString left, ByteString right) => !left.Equals(right);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ByteString left, ByteString right) => left.CompareTo(right) < 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ByteString left, ByteString right) => left.CompareTo(right) <= 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ByteString left, ByteString right) => left.CompareTo(right) > 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ByteString left, ByteString right) => left.CompareTo(right) >= 0;

    // Serialization

#pragma warning disable CS8618
    [Obsolete("Obsolete")]
    private ByteString(SerializationInfo info, StreamingContext context)
        => Bytes = (byte[])info.GetValue(nameof(Bytes), typeof(byte[]))!;
#pragma warning restore CS8618

    [Obsolete("Obsolete")]
    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        => info.AddValue(nameof(Bytes), Bytes.ToArray());
}
