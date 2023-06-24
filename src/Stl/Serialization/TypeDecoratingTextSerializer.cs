using Newtonsoft.Json.Serialization;
using Stl.Serialization.Internal;

namespace Stl.Serialization;

public class TypeDecoratingTextSerializer : TextSerializerBase
{
    public static TypeDecoratingTextSerializer Default { get; } =
        new(SystemJsonSerializer.Default);

    private readonly ISerializationBinder _serializationBinder;

    public ITextSerializer Serializer { get; }
    public Func<Type, bool> TypeFilter { get; }

    public TypeDecoratingTextSerializer(ITextSerializer serializer, Func<Type, bool>? typeFilter = null)
    {
        Serializer = serializer;
        TypeFilter = typeFilter ?? (_ => true);
        var serializationBinder = (Serializer as NewtonsoftJsonSerializer)?.Settings?.SerializationBinder;
#if NET5_0_OR_GREATER
        serializationBinder ??= Internal.SerializationBinder.Instance;
#else
        serializationBinder ??= CrossPlatformSerializationBinder.Instance;
#endif
        _serializationBinder = serializationBinder;
    }

    public override object? Read(string data, Type type)
    {
        using var p = ListFormat.Default.CreateParser(data);

        p.ParseNext();
        if (p.Item.IsNullOrEmpty())
            // Special case: null deserialization
            return null;

        TypeNameHelpers.SplitAssemblyQualifiedName(p.Item, out var assemblyName, out var typeName);
        var actualType = _serializationBinder.BindToType(assemblyName, typeName);
        if (!type.IsAssignableFrom(actualType))
            throw Errors.UnsupportedSerializedType(actualType);
        if (!TypeFilter(actualType))
            throw Errors.UnsupportedSerializedType(actualType);

        p.ParseNext();
        return Serializer.Read(p.Item, actualType);
    }

    public override string Write(object? value, Type type)
    {
        using var f = ListFormat.Default.CreateFormatter();
        if (value == null) {
            // Special case: null serialization
            f.Append("");
            f.AppendEnd();
        }
        else {
            var actualType = value.GetType();
            if (!type.IsAssignableFrom(actualType))
                throw Stl.Internal.Errors.MustBeAssignableTo(actualType, type, nameof(type));
            if (!TypeFilter(actualType))
                throw Errors.UnsupportedSerializedType(actualType);

            var aqn = actualType.GetAssemblyQualifiedName(false, _serializationBinder);
            var json = Serializer.Write(value, actualType);
            f.Append(aqn);
            f.Append(json);
            f.AppendEnd();
        }
        return f.Output;
    }
}
