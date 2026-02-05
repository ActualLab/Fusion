// Source (with light edits):
// - https://github.com/Cysharp/MemoryPack/blob/main/src/MemoryPack.Core/MemoryPackSerializerOptions.cs

#if NETSTANDARD2_0

// ReSharper disable once CheckNamespace
namespace MemoryPack;

/// <summary>
/// Compatibility shim for the MemoryPack <c>MemoryPackSerializerOptions</c> on .NET Standard 2.0.
/// </summary>
public record MemoryPackSerializerOptions
{
    // Default is Utf8
    public static readonly MemoryPackSerializerOptions Default = new() { StringEncoding = StringEncoding.Utf8 };
    public static readonly MemoryPackSerializerOptions Utf8 = Default with { StringEncoding = StringEncoding.Utf8 };
    public static readonly MemoryPackSerializerOptions Utf16 = Default with { StringEncoding = StringEncoding.Utf16 };

    public StringEncoding StringEncoding { get; init; }
    public IServiceProvider? ServiceProvider { get; init; }
}

/// <summary>
/// Compatibility shim for the MemoryPack <c>StringEncoding</c> enum on .NET Standard 2.0.
/// </summary>
#pragma warning disable CA1028
public enum StringEncoding : byte
#pragma warning restore CA1028
{
    Utf16,
    Utf8,
}

#endif
