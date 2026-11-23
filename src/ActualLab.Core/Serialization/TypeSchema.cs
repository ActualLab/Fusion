using ActualLab.Collections.Slim;
using ActualLab.OS;
using ActualLab.Text;
using ActualLab.Time;
using ActualLab.Internal;
using ActualLab.Trimming;

namespace ActualLab.Serialization;

/// <summary>
/// Restricts the set of types a type-decorating serializer is allowed to materialize.
/// </summary>
public abstract class TypeSchema
{
    // Trimming keeps a type's parameterless constructor only if something requires it statically;
    // TypeSchema<TSchema> calls it via Activator.CreateInstance<TSchema>(), which doesn't.
    // A custom TypeSchema must root itself the same way: static MySchema() => CodeKeeper.Keep<MySchema>();
    static TypeSchema()
    {
        CodeKeeper.Keep<Any>();
        CodeKeeper.Keep<PrimitiveOnly>();
    }

    public abstract bool IsAllowed(Type type);

    public sealed class Any : TypeSchema
    {
        public override bool IsAllowed(Type type) => true;
    }

    public sealed class PrimitiveOnly : TypeSchema
    {
        private static readonly HashSet<Type> AllowedTypes = [
            typeof(bool), typeof(char), typeof(string),
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal),
            typeof(Guid), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
            typeof(Moment), typeof(Symbol),
        ];

        public override bool IsAllowed(Type type) => AllowedTypes.Contains(type);
    }
}

/// <summary>
/// Resolves the <see cref="TSchema"/> singleton and the type-decorating serializers bound to it.
/// </summary>
public static class TypeSchema<TSchema>
    where TSchema : TypeSchema
{
    // Indexed by the SerializerKind value
    private static readonly SerializerCache[] Caches = Enumerable.Range(0, SerializerKindExt.MaxKindCount)
        .Select(_ => new SerializerCache())
        .ToArray();

    // Strong references: there are just a few base serializers, and lookups must be fast
    private static readonly ConcurrentDictionary<IByteSerializer, IByteSerializer> BaseSerializerCache
        = new(HardwareInfo.ProcessorCountPo2, 7, ReferenceEqualityComparer<IByteSerializer>.Instance);

    public static readonly TSchema Instance = CreateInstance();

    // null means "no filtering", which lets TypeSchema.Any reuse the shared default serializers as-is
    public static readonly Func<Type, bool>? TypeFilter =
        typeof(TSchema) == typeof(TypeSchema.Any) ? null : Instance.IsAllowed;

    public static IByteSerializer GetTypeDecoratingSerializer(SerializerKind serializerKind)
        => TypeFilter is { } typeFilter
            ? Caches[(int)serializerKind].Get(serializerKind, typeFilter)
            : serializerKind.GetDefaultTypeDecoratingSerializer();

    // Uses the shared SerializerKind path when baseSerializer is that kind's default serializer,
    // so a replaced DefaultTypeDecorating serializer is still honored
    public static IByteSerializer GetTypeDecoratingSerializer(IByteSerializer baseSerializer, SerializerKind serializerKind)
        => ReferenceEquals(baseSerializer, serializerKind.GetDefaultSerializer())
            ? GetTypeDecoratingSerializer(serializerKind)
            : GetTypeDecoratingSerializer(baseSerializer);

    // For serializers that aren't SerializerKind defaults - e.g. the ones format packages own
    public static IByteSerializer GetTypeDecoratingSerializer(IByteSerializer baseSerializer)
        => BaseSerializerCache.GetOrAdd(baseSerializer, static s => CreateTypeDecoratingSerializer(s, TypeFilter));

    // Private methods

    // What "new TSchema()" compiles to. NativeAOT resolves it for each closed TSchema; plain trimming
    // can't, so TSchema's constructor must be rooted - see TypeSchema's static constructor
    [UnconditionalSuppressMessage("Trimming", "IL2091",
        Justification = "TypeSchema descendants root their constructors")]
    private static TSchema CreateInstance()
    {
        try {
            return Activator.CreateInstance<TSchema>();
        }
        catch (MissingMethodException e) {
            throw Errors.ConstructorNotRooted(typeof(TSchema), e);
        }
    }

    private static IByteSerializer CreateTypeDecoratingSerializer(
        IByteSerializer baseSerializer, Func<Type, bool>? typeFilter)
        => baseSerializer is ITextSerializer textSerializer
            ? new TypeDecoratingTextSerializer(textSerializer, typeFilter)
            : new TypeDecoratingByteSerializer(baseSerializer, typeFilter);

    // Nested types

    private sealed class SerializerCache
    {
        private Entry? _entry;

        public IByteSerializer Get(SerializerKind serializerKind, Func<Type, bool> typeFilter)
        {
            // The default serializers are replaceable at runtime, so a cached wrapper is only
            // valid while the serializer it wraps is still the default one.
            var baseSerializer = serializerKind.GetDefaultSerializer();
            if (_entry is { } entry && ReferenceEquals(entry.BaseSerializer, baseSerializer))
                return entry.Serializer;

            var serializer = CreateTypeDecoratingSerializer(baseSerializer, typeFilter);
            Volatile.Write(ref _entry, new Entry(baseSerializer, serializer));
            return serializer;
        }

        private sealed record Entry(IByteSerializer BaseSerializer, IByteSerializer Serializer);
    }
}
