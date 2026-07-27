using ActualLab.Text;
using ActualLab.Time;

namespace ActualLab.Serialization;

/// <summary>
/// Restricts the set of types a type-decorating serializer is allowed to materialize.
/// </summary>
public abstract class TypeSchema
{
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
    where TSchema : TypeSchema, new()
{
    private static readonly SerializerCache[] Caches = [new(), new(), new(), new(), new()];

    public static readonly TSchema Instance = new();

    // null means "no filtering", which lets TypeSchema.Any reuse the shared default serializers as-is
    public static readonly Func<Type, bool>? TypeFilter =
        typeof(TSchema) == typeof(TypeSchema.Any) ? null : Instance.IsAllowed;

    public static IByteSerializer GetTypeDecoratingSerializer(SerializerKind serializerKind)
        => TypeFilter is { } typeFilter
            ? Caches[(int)serializerKind].Get(serializerKind, typeFilter)
            : serializerKind.GetDefaultTypeDecoratingSerializer();

    private sealed class SerializerCache
    {
        private volatile Entry? _entry;

        public IByteSerializer Get(SerializerKind serializerKind, Func<Type, bool> typeFilter)
        {
            // The default serializers are replaceable at runtime, so a cached wrapper is only
            // valid while the serializer it wraps is still the default one.
            var baseSerializer = serializerKind.GetDefaultSerializer();
            if (_entry is { } entry && ReferenceEquals(entry.BaseSerializer, baseSerializer))
                return entry.Serializer;

            IByteSerializer serializer = serializerKind switch {
                SerializerKind.SystemJson or SerializerKind.NewtonsoftJson
                    => new TypeDecoratingTextSerializer((ITextSerializer)baseSerializer, typeFilter),
                _ => new TypeDecoratingByteSerializer(baseSerializer, typeFilter),
            };
            _entry = new Entry(baseSerializer, serializer);
            return serializer;
        }

        private sealed record Entry(IByteSerializer BaseSerializer, IByteSerializer Serializer);
    }
}
