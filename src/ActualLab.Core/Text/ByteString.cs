using System.ComponentModel;
using System.Text;
using ActualLab.Internal;
using ActualLab.IO;
using ActualLab.Text.Internal;
using CommunityToolkit.HighPerformance;
using Cysharp.Text;

namespace ActualLab.Text;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable]
[JsonConverter(typeof(ByteStringJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(ByteStringNewtonsoftJsonConverter))]
[TypeConverter(typeof(ByteStringTypeConverter))]
public readonly partial struct ByteString : IEquatable<ByteString>, IComparable<ByteString>, ISerializable
{
    public static readonly byte[] EmptyBytes = [];
    public static ByteString Empty => new(EmptyBytes);

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public readonly ReadOnlyMemory<byte> Bytes;

    public static ByteString FromBase64(string? base64)
        => base64.IsNullOrEmpty() ? Empty : new(Convert.FromBase64String(base64));
    public static ByteString FromBase64Url(string? base64Url)
        => base64Url.IsNullOrEmpty() ? Empty : new(Base64Encode.Decode(base64Url).ToArray());

    public static ByteString FromStringAsUtf8(string source, int bufferLength = 1)
    {
        bufferLength = Math.Max(bufferLength, source.Length);
        using var buffer = new ArrayPoolBuffer<byte>(bufferLength);
        Encoding.UTF8.GetEncoder().Convert(source.AsSpan(), buffer);
        return buffer.WrittenSpan.ToArray();
    }

    public static ByteString FromStringAsUtf16(string source)
        => source.AsSpan().Cast<char, byte>().ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ByteString(byte[] bytes)
        => Bytes = bytes;

    [MemoryPackConstructor]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ByteString(ReadOnlyMemory<byte> bytes)
        => Bytes = bytes;

    // Conversion

    public override string ToString()
        => ToString(64);
    public string ToString(int maxLength)
        => ZString.Concat("[ ",
            Bytes.Length, " byte(s): ", ToHexString(maxLength),
            Bytes.Length <= maxLength ? " ]" : "... ]");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToBase64()
#if !NETSTANDARD2_0
        => Convert.ToBase64String(Bytes.Span);
#else
        => Convert.ToBase64String(Bytes.ToArray());
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToBase64Url()
        => Base64Encode.Encode(Bytes.Span);

    public string ToHexString(int maxLength = int.MaxValue)
#if NET5_0_OR_GREATER
        => Convert.ToHexString(Bytes.Span[..Math.Min(Bytes.Length, maxLength)]);
#else
        => BitConverter.ToString(Bytes.Span[..Math.Min(Bytes.Length, maxLength)].ToArray());
#endif

    public string ToStringAsUtf8()
        => Encoding.UTF8.GetDecoder().Convert(Bytes.Span);

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
    {
        var span = Bytes.Span;
        var hash = span.Length <= 32
            ? span.GetDjb2HashCode()
            : span[..16].GetDjb2HashCode() + span[^16..].GetDjb2HashCode();
        return (359 * span.Length) + hash;
    }

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

    // Conversion operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ByteString(ReadOnlyMemory<byte> source) => new(source);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ByteString(byte[] source) => new(source);

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
