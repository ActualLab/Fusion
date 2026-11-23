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
        MemoryPackSerializer.Deserialize<T>(ReadOnlySpan<byte>.Empty);
        MemoryPackSerializer.Deserialize<T>(ReadOnlySequence<byte>.Empty);
        MemoryPackSerializer.Serialize<T>(default);
        Next?.KeepSerializable<T>();
    }
}
