using System.ComponentModel;
using Stl.Serialization.Internal;

namespace Stl.Serialization;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[JsonConverter(typeof(Base64EncodedJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Base64EncodedNewtonsoftJsonConverter))]
[TypeConverter(typeof(Base64EncodedTypeConverter))]
public readonly partial struct Base64Encoded : IEquatable<Base64Encoded>, IReadOnlyCollection<byte>
{
    private readonly byte[]? _data;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public byte[] Data => _data ?? Array.Empty<byte>();

    [MemoryPackIgnore]
    public int Count => Data.Length;

    public byte this[int index] {
        get => Data[index];
        set => Data[index] = value;
    }

    [MemoryPackConstructor]
    public Base64Encoded(byte[] data)
        => _data = data;

    public override string ToString()
        => $"{GetType().Name}({Count} byte(s))";

    // IEnumerable
    IEnumerator IEnumerable.GetEnumerator() => Data.GetEnumerator();
    public IEnumerator<byte> GetEnumerator() => (Data as IEnumerable<byte>).GetEnumerator();

    // Conversion
    public string? Encode()
        => Convert.ToBase64String(Data);
    public static Base64Encoded Decode(string? encodedData)
        => encodedData.IsNullOrEmpty() ? Array.Empty<byte>() : Convert.FromBase64String(encodedData);

    // Operators
    public static implicit operator Base64Encoded(byte[] data) => new(data);
    public static implicit operator Base64Encoded(string encodedData) => Decode(encodedData);

    // Equality
    public bool Equals(Base64Encoded other)
        => StructuralComparisons.StructuralEqualityComparer.Equals(Data, other.Data);
    public override bool Equals(object? obj) => obj is Base64Encoded other && Equals(other);
    public override int GetHashCode()
        => StructuralComparisons.StructuralEqualityComparer.GetHashCode(Data);
    public static bool operator ==(Base64Encoded left, Base64Encoded right) => left.Equals(right);
    public static bool operator !=(Base64Encoded left, Base64Encoded right) => !left.Equals(right);
}
