// Source (with light edits):
// - https://github.com/Cysharp/MemoryPack/blob/main/src/MemoryPack.Core/Attributes.cs

using System.IO.Compression;

#if NETSTANDARD2_0

// ReSharper disable once CheckNamespace
namespace MemoryPack;

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackableAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackableAttribute : Attribute
{
    public GenerateType GenerateType { get; }
    public SerializeLayout SerializeLayout { get; }

    // ctor parameter is parsed in MemoryPackGenerator.Parser TypeMeta for detect which ctor used in MemoryPack.Generator.
    // if modify ctor, be careful.

    /// <summary>
    /// [generateType, (VersionTolerant or CircularReference) ? SerializeLayout.Explicit : SerializeLayout.Sequential]
    /// </summary>
    /// <param name="generateType"></param>
    public MemoryPackableAttribute(GenerateType generateType = GenerateType.Object)
    {
        this.GenerateType = generateType;
        this.SerializeLayout = (generateType == GenerateType.VersionTolerant || generateType == GenerateType.CircularReference)
            ? SerializeLayout.Explicit
            : SerializeLayout.Sequential;
    }

    /// <summary>
    /// [GenerateType.Object, serializeLayout]
    /// </summary>
    public MemoryPackableAttribute(SerializeLayout serializeLayout)
    {
        this.GenerateType = GenerateType.Object;
        this.SerializeLayout = serializeLayout;
    }

    public MemoryPackableAttribute(GenerateType generateType, SerializeLayout serializeLayout)
    {
        this.GenerateType = generateType;
        this.SerializeLayout = serializeLayout;
    }
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>GenerateType</c> enum on .NET Standard 2.0.
/// </summary>
public enum GenerateType
{
    Object,
    VersionTolerant,
    CircularReference,
    Collection,
    NoGenerate
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>SerializeLayout</c> enum on .NET Standard 2.0.
/// </summary>
public enum SerializeLayout
{
    Sequential, // default
    Explicit
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackUnionAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
public sealed class MemoryPackUnionAttribute : Attribute
{
    public ushort Tag { get; }
    public Type Type { get; }

    public MemoryPackUnionAttribute(ushort tag, Type type)
    {
        this.Tag = tag;
        this.Type = type;
    }
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackUnionFormatterAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackUnionFormatterAttribute : Attribute
{
    public Type Type { get; }

    public MemoryPackUnionFormatterAttribute(Type type)
    {
        this.Type = type;
    }
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackAllowSerializeAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackAllowSerializeAttribute : Attribute
{
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackOrderAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackOrderAttribute : Attribute
{
    public int Order { get; }

    public MemoryPackOrderAttribute(int order)
    {
        this.Order = order;
    }
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackCustomFormatterAttribute{T}</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public abstract class MemoryPackCustomFormatterAttribute<T> : Attribute
{
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackCustomFormatterAttribute{TFormatter, T}</c>
/// on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public abstract class MemoryPackCustomFormatterAttribute<TFormatter, T> : Attribute
    where TFormatter : class
{
}

// similar naming as System.Text.Json attribtues
// https://docs.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonattribute

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackIgnoreAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackIgnoreAttribute : Attribute
{
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackIncludeAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackIncludeAttribute : Attribute
{
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackConstructorAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackConstructorAttribute : Attribute
{
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackOnSerializingAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackOnSerializingAttribute : Attribute
{
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackOnSerializedAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackOnSerializedAttribute : Attribute
{
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackOnDeserializingAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackOnDeserializingAttribute : Attribute
{
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackOnDeserializedAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MemoryPackOnDeserializedAttribute : Attribute
{
}

// Others

/// <summary>
/// Compatibility shim for the MemoryPack <c>GenerateTypeScriptAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class GenerateTypeScriptAttribute : Attribute
{
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>BrotliFormatterAttribute</c> on .NET Standard 2.0.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class BrotliFormatterAttribute : Attribute
{
    public CompressionLevel CompressionLevel { get; }

    public int Window { get; }

    public int DecompressionSizeLimit { get; }

    public BrotliFormatterAttribute(
        CompressionLevel compressionLevel = CompressionLevel.Fastest,
        int window = 22,
        int decompressionSizeLimit = 134217728)
    {
        this.CompressionLevel = compressionLevel;
        this.Window = window;
        this.DecompressionSizeLimit = decompressionSizeLimit;
    }
}

#endif
