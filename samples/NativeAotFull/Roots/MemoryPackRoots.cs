using ActualLab.Api;
using ActualLab.Compliance;
using ActualLab.Compliance.Internal;
using ActualLab.Serialization.Internal;
using ActualLab.Trimming;
using MemoryPackFormatters = MemoryPack.Formatters;

// MemoryPack resolves the formatters of closed generic types via MakeGenericType too, and ActualLab.Generators
// doesn't cover it. SerializableTypeRoots.Keep roots the generic method that installs ActualLab's ones.
// Like CodeKeeper's own: the roots never execute, they only make the AOT compiler keep the code
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The roots only keep the code")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The roots only keep the code")]
public static class MemoryPackRoots
{
    public static void Keep()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        // ActualLab.Serialization.MemoryPack's formatters, which MemoryPackSerialization.Register closes;
        // KeepSerializable<T> (see KeepSerializableTypes) roots the generic method that installs them
        CodeKeeper.Keep<OptionMemoryPackFormatter<int>>();
        CodeKeeper.Keep<ApiArrayMemoryPackFormatter<int>>();
        CodeKeeper.Keep<ApiOptionMemoryPackFormatter<int>>();
        CodeKeeper.Keep<ApiNullableMemoryPackFormatter<int>>();
        CodeKeeper.Keep<ApiNullable8MemoryPackFormatter<long>>();
        CodeKeeper.Keep<SanitizedStringMemoryPackFormatter<Sanitizers.Hidden>>();
        CodeKeeper.Keep<SanitizedStringMemoryPackFormatter<CustomSanitizer>>();
        // MemoryPack's own generic formatters
        CodeKeeper.Keep<MemoryPackFormatters.ArrayFormatter<Moment>>();
        CodeKeeper.Keep<MemoryPackFormatters.ListFormatter<int>>();
        CodeKeeper.Keep<MemoryPackFormatters.ListFormatter<Moment>>();
        CodeKeeper.Keep<MemoryPackFormatters.DictionaryFormatter<string, Moment>>();
        // MemoryPack serializes unmanaged structs like Moment as raw bytes
        CodeKeeper.Keep<MemoryPackFormatters.DangerousUnmanagedFormatter<Moment>>();
    }
}
