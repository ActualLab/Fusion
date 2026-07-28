using System.ComponentModel;
using System.Text.RegularExpressions;
using ActualLab.Internal;
using ActualLab.Reflection.Internal;
using MessagePack;
using Errors = ActualLab.Reflection.Internal.Errors;

namespace ActualLab.Reflection;

/// <summary>
/// A serializable reference to a <see cref="Type"/> by its assembly-qualified name.
/// </summary>
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
    public const int DefaultUnversionedNameCacheCapacity = 16384;
    public const int DefaultResolveCacheCapacity = 16384;

    private static volatile PruningCache<string, TypeRef> _unversionedNameCache
        = NewUnversionedNameCache(DefaultUnversionedNameCacheCapacity);
    private static volatile PruningCache<string, Type> _resolveCache
        = NewResolveCache(DefaultResolveCacheCapacity);

    public static readonly TypeRef None = default;

    public static int UnversionedNameCacheCapacity {
        get => _unversionedNameCache.Capacity;
        set => _unversionedNameCache = NewUnversionedNameCache(value);
    }
    public static int UnversionedNameCacheSize => _unversionedNameCache.Count;
    public static int ResolveCacheCapacity {
        get => _resolveCache.Capacity;
        set => _resolveCache = NewResolveCache(value);
    }
    public static int ResolveCacheSize => _resolveCache.Count;

    [DataMember(Order = 0), MemoryPackOrder(0), StringAsSymbolMemoryPackFormatter, Key(0)]
    public string AssemblyQualifiedName => field ?? "";

    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore, IgnoreMember]
    public string TypeName {
        get {
            var name = AssemblyQualifiedName;
            var separatorIndex = name.IndexOf(',', StringComparison.Ordinal);
            return separatorIndex < 0 ? name : name[..separatorIndex];
        }
    }

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
    {
        var name = AssemblyQualifiedName;
        var cache = _unversionedNameCache;
        if (cache.TryGet(name, out var result))
            return result;

        result = new TypeRef(RemoveAssemblyVersionsRe.Replace(name, ""));
        cache.TryAdd(name, result);
        return result;
    }

    public bool IsAcceptableName(int maxGenericArity, string[] nameSuffixes)
    {
        // A purely structural check on the type name portion - no type is looked up, so it's safe
        // to run it on a name that came from an untrusted source *before* Resolve materializes
        // runtime types in the loader heap and probes for assemblies.
        // Arrays, pointers and by-refs are rejected as well: their names never end with a suffix.
        var name = AssemblyQualifiedName.AsSpan();
        var depth = 0;
        var typeNameLength = name.Length;
        for (var i = 0; i < name.Length; i++) {
            var c = name[i];
            if (c == '[')
                depth++;
            else if (c == ']') {
                if (--depth < 0)
                    return false;
            }
            else if (c == ',' && depth == 0) {
                typeNameLength = i;
                break;
            }
        }

        var typeName = name[..typeNameLength].Trim();
        if (typeName.IsEmpty)
            return false;

        var arity = 0;
        var rootArity = 0;
        var argumentsStart = -1;
        var argumentsEnd = -1;
        depth = 0;
        for (var i = 0; i < typeName.Length; i++) {
            switch (typeName[i]) {
            case '`':
                var digitCount = 0;
                var itemArity = 0;
                while (i + 1 < typeName.Length && typeName[i + 1] is >= '0' and <= '9') {
                    if (++digitCount > 2)
                        return false;

                    itemArity = (itemArity * 10) + (typeName[++i] - '0');
                }
                if (digitCount == 0)
                    return false;

                arity += itemArity;
                if (arity > maxGenericArity)
                    return false;

                if (depth == 0)
                    rootArity += itemArity;
                break;
            case '[':
                if (depth++ == 0) {
                    if (argumentsStart >= 0)
                        return false;

                    argumentsStart = i;
                }
                break;
            case ']':
                if (--depth < 0)
                    return false;

                if (depth == 0)
                    argumentsEnd = i;
                break;
            case '*':
            case '&':
                return false;
            }
        }
        if (depth != 0)
            return false;

        var rootName = typeName;
        if (argumentsStart >= 0) {
            // The only bracket block we accept is a generic argument list closing the type name
            if (argumentsEnd != typeName.Length - 1 || rootArity == 0)
                return false;

            rootName = typeName[..argumentsStart].TrimEnd();
        }
        rootName = TrimTrailingArity(rootName);
        foreach (var suffix in nameSuffixes)
            if (rootName.EndsWith(suffix, StringComparison.Ordinal))
                return true;

        return false;
    }

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
        var resolveCache = _resolveCache;
        if (resolveCache.TryGet(assemblyQualifiedName, out var cachedType))
            return cachedType;

        // Failures are never cached: an assembly loaded later must still be resolvable
        var type = Type.GetType(assemblyQualifiedName, false, false);
        if (type is null)
            return null;

        // Type.GetType tolerates assembly name casing, extra whitespace and partial qualification,
        // so the same type has many spellings - and every wire-supplied one would otherwise become
        // a permanent cache entry. Only the canonical spelling this library itself emits is cached.
        if (!string.Equals(GetCanonicalName(type), assemblyQualifiedName, StringComparison.Ordinal))
            return type;

        resolveCache.TryAdd(assemblyQualifiedName, type);
        return type;
    }

    private static string GetCanonicalName(Type type)
        // Deliberately not routed through WithoutAssemblyVersions(): its cache is unbounded,
        // and a peer streaming distinct generic instantiations would grow it without limit
        => RemoveAssemblyVersionsRe.Replace(type.AssemblyQualifiedName ?? "", "");

    private static PruningCache<string, TypeRef> NewUnversionedNameCache(int capacity)
        => new(capacity, StringComparer.Ordinal);

    private static PruningCache<string, Type> NewResolveCache(int capacity)
        => new(capacity, StringComparer.Ordinal);

    private static ReadOnlySpan<char> TrimTrailingArity(ReadOnlySpan<char> name)
    {
        var index = name.Length;
        while (index > 0 && name[index - 1] is >= '0' and <= '9')
            index--;

        return index > 0 && index < name.Length && name[index - 1] == '`'
            ? name[..(index - 1)]
            : name;
    }

    // Serialization

    private TypeRef(SerializationInfo info, StreamingContext context)
        => AssemblyQualifiedName = info.GetString(nameof(AssemblyQualifiedName)) ?? "";

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        => info.AddValue(nameof(AssemblyQualifiedName), AssemblyQualifiedName);
}
