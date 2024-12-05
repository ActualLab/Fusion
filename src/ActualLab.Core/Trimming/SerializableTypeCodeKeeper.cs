using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Trimming;

#pragma warning disable IL2026

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
