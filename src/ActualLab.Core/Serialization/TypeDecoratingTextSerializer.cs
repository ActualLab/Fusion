using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Serialization;
using ActualLab.Internal;
using ActualLab.Serialization.Internal;
using Errors = ActualLab.Serialization.Internal.Errors;

namespace ActualLab.Serialization;

#if NET6_0_OR_GREATER
[RequiresUnreferencedCode(UnreferencedCode.Serialization)]
#endif
public class TypeDecoratingTextSerializer : TextSerializerBase
{
    public static TypeDecoratingTextSerializer Default { get; set; }

    private readonly ISerializationBinder _serializationBinder;

    public ITextSerializer Serializer { get; }
    public Func<Type, bool> TypeFilter { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
#pragma warning disable IL2116
    static TypeDecoratingTextSerializer()
#pragma warning restore IL2116
#pragma warning disable IL2026
        => Default = new(SystemJsonSerializer.Default);
#pragma warning restore IL2026

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
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

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
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
        if (!TypeFilter.Invoke(actualType))
            throw Errors.UnsupportedSerializedType(actualType);

        p.ParseNext();
        return Serializer.Read(p.Item, actualType);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
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
                throw ActualLab.Internal.Errors.MustBeAssignableTo(actualType, type, nameof(type));
            if (!TypeFilter.Invoke(actualType))
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
