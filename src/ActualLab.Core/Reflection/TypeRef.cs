using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ActualLab.Internal;
using ActualLab.OS;
using ActualLab.Reflection.Internal;
using MessagePack;
using Errors = ActualLab.Reflection.Internal.Errors;

namespace ActualLab.Reflection;

[StructLayout(LayoutKind.Auto)]
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackFormatter(typeof(TypeRefMessagePackFormatter))]
[JsonConverter(typeof(TypeRefJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(TypeRefNewtonsoftJsonConverter))]
[TypeConverter(typeof(TypeRefTypeConverter))]
public readonly partial struct TypeRef : IEquatable<TypeRef>, IComparable<TypeRef>, ISerializable
{
#if NET7_0_OR_GREATER
    [GeneratedRegex(@",\s+Version=[^,]*,\s+Culture=[^,]*,\s+PublicKeyToken=[A-Za-z0-9]+")]
    private static partial Regex RemoveAssemblyVersionsReFactory();
    private static readonly Regex RemoveAssemblyVersionsRe = RemoveAssemblyVersionsReFactory();
#else
    private static readonly Regex RemoveAssemblyVersionsRe =
        new(@",\s+Version=[^,]*,\s+Culture=[^,]*,\s+PublicKeyToken=[A-Za-z0-9]+", RegexOptions.Compiled);
#endif
    private static readonly ConcurrentDictionary<string, TypeRef> UnversionedAssemblyNameCache
        = new(HardwareInfo.ProcessorCountPo2, 131, StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Type?> ResolveCache
        = new(HardwareInfo.ProcessorCountPo2, 131, StringComparer.Ordinal);

    public static readonly TypeRef None = default;

    [DataMember(Order = 0), MemoryPackOrder(0), StringAsSymbolMemoryPackFormatter, Key(0)]
    public string AssemblyQualifiedName => field ?? "";

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public string TypeName
        => AssemblyQualifiedName[..AssemblyQualifiedName.IndexOf(',', StringComparison.Ordinal)];

    public TypeRef(Type type)
        : this(type.AssemblyQualifiedName!) { }
    [JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor, SerializationConstructor]
    public TypeRef(string assemblyQualifiedName)
        => AssemblyQualifiedName = assemblyQualifiedName;

    public override string ToString()
        => AssemblyQualifiedName;

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public Type? TryResolve()
        => Resolve(AssemblyQualifiedName);
    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public Type Resolve()
        => Resolve(AssemblyQualifiedName) ?? throw Errors.TypeNotFound(AssemblyQualifiedName);

    public TypeRef WithoutAssemblyVersions()
        => UnversionedAssemblyNameCache.GetOrAdd(AssemblyQualifiedName,
            static aqn => new(RemoveAssemblyVersionsRe.Replace(aqn, "")));

    // Conversion

    public static implicit operator TypeRef(string typeName) => new(typeName);
    public static implicit operator TypeRef(Type type) => new(type.AssemblyQualifiedName!);
    public static explicit operator string(TypeRef type) => type.AssemblyQualifiedName;
    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static explicit operator Type(TypeRef type) => type.Resolve();

    // Equality & comparison

    public bool Equals(TypeRef other)
        => string.Equals(AssemblyQualifiedName, other.AssemblyQualifiedName, StringComparison.Ordinal);
    public override bool Equals(object? obj)
        => obj is TypeRef other && Equals(other);
    public override int GetHashCode()
        => AssemblyQualifiedName.GetOrdinalHashCode();
    public int CompareTo(TypeRef other)
        => string.CompareOrdinal(AssemblyQualifiedName, other.AssemblyQualifiedName);

    public static bool operator ==(TypeRef left, TypeRef right) => left.Equals(right);
    public static bool operator !=(TypeRef left, TypeRef right) => !left.Equals(right);
    public static bool operator <(TypeRef left, TypeRef right) => left.CompareTo(right) < 0;
    public static bool operator <=(TypeRef left, TypeRef right) => left.CompareTo(right) <= 0;
    public static bool operator >(TypeRef left, TypeRef right) => left.CompareTo(right) > 0;
    public static bool operator >=(TypeRef left, TypeRef right) => left.CompareTo(right) >= 0;

    // Private methods

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static Type? Resolve(string assemblyQualifiedName)
    {
        var result = ResolveCache.GetOrAdd(assemblyQualifiedName,
            static aqn => Type.GetType(aqn, false, false));
        if (result == null)
            ResolveCache.TryRemove(assemblyQualifiedName, out _); // Potential memory lead / attack vector
        return result;
    }

    // Serialization

    private TypeRef(SerializationInfo info, StreamingContext context)
        => AssemblyQualifiedName = info.GetString(nameof(AssemblyQualifiedName)) ?? "";

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        => info.AddValue(nameof(AssemblyQualifiedName), AssemblyQualifiedName);
}
