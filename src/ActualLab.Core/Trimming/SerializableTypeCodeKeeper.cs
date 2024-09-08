using System.Buffers;

namespace ActualLab.Trimming;

public class SerializableTypeCodeKeeper : CodeKeeper
{
    public virtual T KeepType<T>()
    {
        var value = Keep<T>(ensureInitialized: true); // It has to be initialized to register MemoryPack formatters
        if (AlwaysTrue)
            return value;

        Keep<UniSerialized<T>>();
        Keep<MemoryPackSerialized<T>>();
        Keep<MemoryPackByteSerializer<T>>();
        CallSilently(() => MemoryPackSerializer.Deserialize<T>(ReadOnlySpan<byte>.Empty));
        CallSilently(() => MemoryPackSerializer.Deserialize<T>(ReadOnlySequence<byte>.Empty));
        CallSilently(() => MemoryPackSerializer.Serialize<T>(default));
        return value;
    }
}
