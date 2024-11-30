using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Serialization;
using ActualLab.Internal;
using ActualLab.Serialization.Internal;
using Errors = ActualLab.Serialization.Internal.Errors;

namespace ActualLab.Serialization;

#pragma warning disable IL2116, IL2026

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Serialization)]
#endif
[method: RequiresUnreferencedCode(UnreferencedCode.Serialization)]
public class TypeDecoratingTextSerializer(ITextSerializer serializer, Func<Type, bool>? typeFilter = null)
    : TextSerializerBase
{
    private static readonly Lock StaticLock = LockFactory.Create();

    public const string TypeDecoratorPrefix = "/* @type ";
    public const string TypeDecoratorSuffix = " */ ";
    public const char ExactTypeDecorator = '.';

    [field: AllowNull, MaybeNull]
    public static TypeDecoratingTextSerializer Default {
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new(TextSerializer.Default);
        }
        set {
            lock (StaticLock)
                field = value;
        }
    }

    [field: AllowNull, MaybeNull]
    public static TypeDecoratingTextSerializer DefaultLegacy {
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new LegacyTypeDecoratingTextSerializer(TextSerializer.Default);
        }
        set {
            lock (StaticLock)
                field = value;
        }
    }

    public ITextSerializer Serializer { get; } = serializer;
    public Func<Type, bool> TypeFilter { get; } = typeFilter ?? (static _ => true);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override object? Read(string data, Type type)
    {
        if (data.IsNullOrEmpty())
            return null;
        if (!data.StartsWith(TypeDecoratorPrefix, StringComparison.Ordinal))
            return ReadLegacy(data, type);

        var typeSuffixIndex = data.IndexOf(TypeDecoratorSuffix, TypeDecoratorPrefix.Length, StringComparison.Ordinal);
        if (typeSuffixIndex < 0)
            throw Errors.WrongTypeDecoratorFormat();

        var aqn = data.Substring(TypeDecoratorPrefix.Length, typeSuffixIndex - TypeDecoratorPrefix.Length);
        var tail = data[(typeSuffixIndex + TypeDecoratorSuffix.Length)..];
        if (aqn is [ExactTypeDecorator])
            return Serializer.Read(tail, type);

        var actualType = new TypeRef(aqn).Resolve();
        if (!type.IsAssignableFrom(actualType))
            throw Errors.UnsupportedSerializedType(actualType);
        if (!TypeFilter.Invoke(actualType))
            throw Errors.UnsupportedSerializedType(actualType);

        return Serializer.Read(tail, actualType);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override string Write(object? value, Type type)
    {
        if (value == null)
            return "";

        var sb = StringBuilderExt.Acquire();
        try {
            sb.Append(TypeDecoratorPrefix);
            var actualType = value.GetType();
            if (actualType == type)
                sb.Append(ExactTypeDecorator);
            else
                sb.Append(new TypeRef(actualType).WithoutAssemblyVersions());
            sb.Append(TypeDecoratorSuffix);
            sb.Append(Serializer.Write(value, actualType));
            return sb.ToString();
        }
        finally {
            sb.Release();
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected object? ReadLegacy(string data, Type type)
    {
        using var p = ListFormat.Default.CreateParser(data);

        p.ParseNext();
        if (p.Item.IsNullOrEmpty())
            // Special case: null deserialization
            return null;

        TypeNameHelpers.SplitAssemblyQualifiedName(p.Item, out var assemblyName, out var typeName);
        var actualType = NewtonsoftJsonSerializationBinder.Default.BindToType(assemblyName, typeName);
        if (!type.IsAssignableFrom(actualType))
            throw Errors.UnsupportedSerializedType(actualType);
        if (!TypeFilter.Invoke(actualType))
            throw Errors.UnsupportedSerializedType(actualType);

        p.ParseNext();
        return Serializer.Read(p.Item, actualType);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    protected string WriteLegacy(object? value, Type type)
    {
        if (value == null)
            return "";

        using var f = ListFormat.Default.CreateFormatter();
        var actualType = value.GetType();
        var aqn = actualType.GetAssemblyQualifiedName(false, NewtonsoftJsonSerializationBinder.Default);
        var data = Serializer.Write(value, actualType);
        f.Append(aqn);
        f.Append(data);
        f.AppendEnd();
        return f.Output;
    }
}

public class LegacyTypeDecoratingTextSerializer(ITextSerializer serializer, Func<Type, bool>? typeFilter = null)
    : TypeDecoratingTextSerializer(serializer, typeFilter)
{
    public override object? Read(string data, Type type)
        => ReadLegacy(data, type);

    public override string Write(object? value, Type type)
        => WriteLegacy(value, type);
}
