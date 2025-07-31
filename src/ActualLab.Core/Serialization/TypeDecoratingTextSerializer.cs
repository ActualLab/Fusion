using System.Diagnostics.CodeAnalysis;
using ActualLab.Serialization.Internal;
using Errors = ActualLab.Serialization.Internal.Errors;

namespace ActualLab.Serialization;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume you know serialization may involve reflection and dynamic invocations")]
[UnconditionalSuppressMessage("Trimming", "IL2116", Justification = "We assume you know serialization may involve reflection and dynamic invocations")]
public class TypeDecoratingTextSerializer(ITextSerializer serializer, Func<Type, bool>? typeFilter = null)
    : TextSerializerBase
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static volatile TypeDecoratingTextSerializer? _default;
    private static volatile TypeDecoratingTextSerializer? _defaultLegacy;

    public const string TypeDecoratorPrefix = "/* @type ";
    public const string TypeDecoratorSuffix = " */ ";
    public const char ExactTypeDecorator = '.';

    public static TypeDecoratingTextSerializer Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _default ??= new(TextSerializer.Default);
        }
        set {
            lock (StaticLock)
                _default = value;
        }
    }

    public static TypeDecoratingTextSerializer DefaultLegacy {
        get {
            if (_defaultLegacy is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _defaultLegacy ??= new LegacyTypeDecoratingTextSerializer(TextSerializer.Default);
        }
        set {
            lock (StaticLock)
                _defaultLegacy = value;
        }
    }

    public ITextSerializer Serializer { get; } = serializer;
    public Func<Type, bool> TypeFilter { get; } = typeFilter ?? (static _ => true);

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

    public override string Write(object? value, Type type)
    {
        if (value is null)
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

    protected string WriteLegacy(object? value, Type type)
    {
        if (value is null)
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
