using System.Buffers;

namespace ActualLab.Trimming;

/// <summary>
/// A <see cref="CodeKeeper"/> that retains serializable types and their serialization infrastructure
/// to prevent trimming.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class SerializableTypeCodeKeeper : CodeKeeper
{
    public virtual T KeepType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    {
        var value = Keep<T>(ensureInitialized: true); // It has to be initialized to register MemoryPack formatters
        if (AlwaysTrue)
            return value;

        Keep<UniSerialized<T>>();
        Keep<MemoryPackSerialized<T>>();
        Keep<MemoryPackByteSerializer<T>>();
#if !NETSTANDARD2_0
        CallSilently(() => MemoryPackSerializer.Deserialize<T>(ReadOnlySpan<byte>.Empty));
        CallSilently(() => MemoryPackSerializer.Deserialize<T>(ReadOnlySequence<byte>.Empty));
        CallSilently(() => MemoryPackSerializer.Serialize<T>(default));
#endif
        return value;
    }
}
