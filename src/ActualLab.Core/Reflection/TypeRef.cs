#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#else
using System.Collections.ObjectModel;
#endif
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
[DataContract, MessagePackFormatter(typeof(TypeRefMessagePackFormatter))]
[JsonConverter(typeof(TypeRefJsonConverter))]
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

#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif

    private static PruningCache<string, TypeRef> _unversionedNameCache
        = NewUnversionedNameCache(DefaultUnversionedNameCacheCapacity);
    private static PruningCache<string, Type> _resolveCache
        = NewResolveCache(DefaultResolveCacheCapacity);

    public static readonly TypeRef None = default;

    public static int UnversionedNameCacheCapacity {
        get => _unversionedNameCache.Capacity;
        set {
            lock (StaticLock)
                // Release, so the new cache is fully constructed before the reference is visible;
                // readers stay plain - they reach its fields through this reference
                Volatile.Write(ref _unversionedNameCache, NewUnversionedNameCache(value));
        }
    }
    public static int UnversionedNameCacheSize => _unversionedNameCache.Count;
    public static int ResolveCacheCapacity {
        get => _resolveCache.Capacity;
        set {
            lock (StaticLock)
                Volatile.Write(ref _resolveCache, NewResolveCache(value));
        }
    }
    public static int ResolveCacheSize => _resolveCache.Count;

#if NET8_0_OR_GREATER
    private static FrozenDictionary<string, string> _movedTypes
#else
    private static ReadOnlyDictionary<string, string> _movedTypes
#endif
        = ToMovedTypes(new Dictionary<string, string>(StringComparer.Ordinal) {
            ["ActualLab.Serialization.NewtonsoftJsonSerialized`1"] = "ActualLab.Serialization.NewtonsoftJson",
            ["ActualLab.Serialization.TypeDecoratingNewtonsoftJsonSerialized`2"] = "ActualLab.Serialization.NewtonsoftJson",
            ["ActualLab.Serialization.MemoryPackSerialized`1"] = "ActualLab.Serialization.MemoryPack",
            ["ActualLab.Serialization.TypeDecoratingMemoryPackSerialized`2"] = "ActualLab.Serialization.MemoryPack",
            ["ActualLab.Api.ApiOption`1"] = "ActualLab.Serialization.MemoryPack",
            ["ActualLab.Api.ApiNullable`1"] = "ActualLab.Serialization.MemoryPack",
            ["ActualLab.Api.ApiNullable8`1"] = "ActualLab.Serialization.MemoryPack",
            ["ActualLab.Collections.OptionSet"] = "ActualLab.Core.Legacy",
            ["ActualLab.Collections.ImmutableOptionSet"] = "ActualLab.Core.Legacy",
        });

    // Top-level type full name (with the generic arity suffix, e.g. "Ns.Foo`1") -> the assembly it moved to;
    // nested types follow their declaring type's entry. Resolves type names written before the move:
    // a type forwarder can't point to a dependent assembly.
    public static IReadOnlyDictionary<string, string> MovedTypes {
        get => _movedTypes;
        set {
            var movedTypes = ToMovedTypes(value);
            lock (StaticLock)
                // Release, so the new dictionary is fully constructed before the reference is visible
                Volatile.Write(ref _movedTypes, movedTypes);
        }
    }

    [DataMember(Order = 0), Key(0)]
    public string AssemblyQualifiedName => field ?? "";

    [JsonIgnore, IgnoreDataMember, IgnoreMember]
    public string TypeName {
        get {
            var name = AssemblyQualifiedName;
            var separatorIndex = name.IndexOf(',', StringComparison.Ordinal);
            return separatorIndex < 0 ? name : name[..separatorIndex];
        }
    }

    public TypeRef(Type type)
        : this(type.AssemblyQualifiedName!) { }
    [JsonConstructor, SerializationConstructor]
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

    // An atomic read-modify-write of MovedTypes, so concurrent registrations don't lose each other
    public static void AddMovedType(string typeName, string assemblyName)
    {
        lock (StaticLock) {
            var movedTypes = _movedTypes.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            movedTypes[typeName] = assemblyName;
            Volatile.Write(ref _movedTypes, ToMovedTypes(movedTypes));
        }
    }

#if NET8_0_OR_GREATER
    private static FrozenDictionary<string, string> ToMovedTypes(IReadOnlyDictionary<string, string> movedTypes)
        => movedTypes.ToFrozenDictionary(StringComparer.Ordinal);
#else
    private static ReadOnlyDictionary<string, string> ToMovedTypes(IReadOnlyDictionary<string, string> movedTypes)
        => new(movedTypes.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal));
#endif

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static Type? Resolve(string assemblyQualifiedName)
    {
        var resolveCache = _resolveCache;
        if (resolveCache.TryGet(assemblyQualifiedName, out var cachedType))
            return cachedType;

        // Failures are never cached: an assembly loaded later must still be resolvable
#if NETSTANDARD2_0
        var type = Type.GetType(assemblyQualifiedName, ResolveAssembly, ResolveType, false, false);
#else
        var type = Type.GetType(assemblyQualifiedName, null, ResolveType, false, false);
#endif
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

    // Payloads written before v16 name these types with their old assembly (ActualLab.Core), and a type
    // forwarder can't point from ActualLab.Core to the packages depending on it
    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    private static Type? ResolveType(Assembly? assembly, string typeName, bool ignoreCase)
    {
        var type = assembly is null
            ? Type.GetType(typeName, false, ignoreCase)
            : assembly.GetType(typeName, false, ignoreCase);
        if (type is not null || !_movedTypes.TryGetValue(typeName, out var newAssemblyName))
            return type;

        try {
            return Assembly.Load(new AssemblyName(newAssemblyName)).GetType(typeName, false, ignoreCase);
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException or BadImageFormatException) {
            return null;
        }
    }

    private static string GetCanonicalName(Type type)
        // Deliberately not routed through WithoutAssemblyVersions(): its cache is unbounded,
        // and a peer streaming distinct generic instantiations would grow it without limit
        => RemoveAssemblyVersionsRe.Replace(type.AssemblyQualifiedName ?? "", "");

#if NETSTANDARD2_0
    // .NET Framework can't load a GAC assembly by its simple name (e.g. "System.Core"),
    // which is how the unversioned type names this library emits refer to it
    private static Assembly? ResolveAssembly(AssemblyName assemblyName)
    {
        // ReferenceMatchesDefinition compares simple names only, so a name with a version, a public key token
        // or a specific culture is left to the binder
        if (assemblyName.Version is null
            && assemblyName.GetPublicKeyToken() is not { Length: > 0 }
            && string.IsNullOrEmpty(assemblyName.CultureName))
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (AssemblyName.ReferenceMatchesDefinition(assemblyName, assembly.GetName()))
                    return assembly;

        try {
            return Assembly.Load(assemblyName);
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException or BadImageFormatException) {
            return null;
        }
    }
#endif

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
