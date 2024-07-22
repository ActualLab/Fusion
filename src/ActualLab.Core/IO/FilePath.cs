using System.ComponentModel;
using ActualLab.Internal;
using ActualLab.IO.Internal;

namespace ActualLab.IO;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[JsonConverter(typeof(FilePathJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(FilePathNewtonsoftJsonConverter))]
[TypeConverter(typeof(FilePathTypeConverter))]
public readonly partial struct FilePath : IEquatable<FilePath>, IComparable<FilePath>
{
    public static readonly FilePath Empty = new("");

    private readonly string? _value;

    [DataMember(Order = 0), MemoryPackOrder(0)]
    public string Value => _value ?? "";

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public int Length => Value.Length;
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public bool IsEmpty => _value.IsNullOrEmpty();
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
#if !NETSTANDARD2_0
    public bool IsFullyQualified => Path.IsPathFullyQualified(Value);
#else
    public bool IsFullyQualified => PathCompatExt.IsPathFullyQualified(Value);
#endif
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public bool IsRooted => Path.IsPathRooted(Value);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public bool HasExtension => Path.HasExtension(Value);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public string Extension => Path.GetExtension(Value);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public FilePath DirectoryPath => Path.GetDirectoryName(Value);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public FilePath FileName => Path.GetFileName(Value);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public FilePath FileNameWithoutExtension => Path.GetFileNameWithoutExtension(Value);
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public FilePath FullPath => Path.GetFullPath(Value);

    public static FilePath New(string? value) => new(value ?? "");

    [MemoryPackConstructor]
    public FilePath(string? value) => _value = value;

    // Conversion
    public override string ToString() => Value;
    public static implicit operator FilePath(string? source) => new(source);
    public static implicit operator string(FilePath source) => source.Value;

    // Operators
    public static FilePath operator +(FilePath p1, FilePath p2)
        => p1.Value + p2.Value;
    public static FilePath operator |(FilePath p1, FilePath p2)
        => JoinOrTakeSecond(p1.Value, p2.Value);
    public static FilePath operator &(FilePath p1, FilePath p2)
        => Join(p1.Value, p2.Value);

    // Equality
    public bool Equals(FilePath other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is FilePath other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
    public static bool operator ==(FilePath left, FilePath right) => left.Equals(right);
    public static bool operator !=(FilePath left, FilePath right) => !left.Equals(right);
    public static bool operator <(FilePath left, FilePath right) => left.CompareTo(right) < 0;
    public static bool operator <=(FilePath left, FilePath right) => left.CompareTo(right) <= 0;
    public static bool operator >(FilePath left, FilePath right) => left.CompareTo(right) > 0;
    public static bool operator >=(FilePath left, FilePath right) => left.CompareTo(right) >= 0;

    // Comparison
    public int CompareTo(FilePath other)
        => string.Compare(Value, other.Value, StringComparison.Ordinal);

    public FilePath ChangeExtension(string newExtension) => Path.ChangeExtension(Value, newExtension);
#if !NETSTANDARD2_0
    public FilePath RelativeTo(FilePath relativeTo) => Path.GetRelativePath(relativeTo, Value);
#else
    public FilePath RelativeTo(FilePath relativeTo) => PathCompatExt.GetRelativePath(relativeTo, Value);
#endif

    public FilePath Normalize() => Value
        .Replace('\\', Path.DirectorySeparatorChar)
        .Replace('/', Path.DirectorySeparatorChar);

    public FilePath ToAbsolute(FilePath? basePath = null)
    {
        if (basePath != null)
#if !NETSTANDARD2_0
            return Path.GetFullPath(Value, basePath.Value);
#else
            return PathCompatExt.GetFullPath(Value, basePath.Value);
#endif
        if (!IsFullyQualified)
            throw Errors.PathIsRelative(null);
        return Path.GetFullPath(Value);
    }

    public static FilePath JoinOrTakeSecond(string s1, string s2)
        => Path.Combine(s1, s2);

    public static FilePath Join(string s1, string s2)
#if !NETSTANDARD2_0
        => s2.IsNullOrEmpty()
            ? s1
            : Path.IsPathFullyQualified(s2)
                ? throw new ArgumentOutOfRangeException(s2)
                : Path.Join(s1, s2);
#else
        => s2.IsNullOrEmpty()
            ? s1
            : PathCompatExt.IsPathFullyQualified(s2)
                ? throw new ArgumentOutOfRangeException(s2)
                : PathCompatExt.Join(s1, s2);
#endif
}
