using System.Buffers;
using ActualLab.Serialization.Internal;
using ActualLab.Trimming;

namespace ActualLab.Serialization.Trimming;

/// <summary>
/// A <see cref="CodeKeeper.IExtension"/> retaining the MemoryPack serialization code of the types
/// <see cref="CodeKeeper.KeepSerializable{T}"/> is called for. Calls the extension it replaces as well.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public sealed class MemoryPackCodeKeeperExtension(CodeKeeper.IExtension? next) : CodeKeeper.IExtension
{
    public CodeKeeper.IExtension? Next { get; } = next;

    public void Keep<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
        => Next?.Keep<T>();

    public void Keep([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        => Next?.Keep(type);

    public void Keep(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] string assemblyQualifiedTypeName)
        => Next?.Keep(assemblyQualifiedTypeName);

    public void KeepSerializable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep<MemoryPackSerialized<T>>();
        CodeKeeper.Keep<MemoryPackByteSerializer<T>>();
        // MemoryPackSerialization.Register<T> instantiates these via MakeGenericType
        CodeKeeper.Keep<MemoryPackSerializedMemoryPackFormatter<T>>();
        CodeKeeper.Keep<UniSerializedMemoryPackFormatter<T>>();
        CodeKeeper.Keep<OptionMemoryPackFormatter<T>>();
        CodeKeeper.Keep<ApiOptionMemoryPackFormatter<T>>();
        CodeKeeper.Keep<ResultMemoryPackFormatter<T>>();
        // MemoryPackSerialization.Register installs the closed formatters via MakeGenericMethod, closing it
        // over T and over the generic types it registers formatters for. A struct one needs its own
        // instantiation per value type argument, so the wrappers of T are rooted here; any other closed
        // generic type the app serializes needs its own KeepSerializable call.
        MemoryPackSerialization.RegisterClosedFormatter<T>(null!);
        MemoryPackSerialization.RegisterClosedFormatter<Option<T>>(null!);
        MemoryPackSerialization.RegisterClosedFormatter<ApiOption<T>>(null!);
        MemoryPackSerialization.RegisterClosedFormatter<Result<T>>(null!);
        MemoryPackSerialization.RegisterClosedFormatter<UniSerialized<T>>(null!);
        MemoryPackSerializer.Deserialize<T>(ReadOnlySpan<byte>.Empty);
        MemoryPackSerializer.Deserialize<T>(ReadOnlySequence<byte>.Empty);
        MemoryPackSerializer.Serialize<T>(default);
        Next?.KeepSerializable<T>();
    }
}
