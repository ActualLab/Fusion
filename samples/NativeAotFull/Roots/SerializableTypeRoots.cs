using ActualLab.Api;
using ActualLab.Compliance;
using ActualLab.Trimming;

// CodeKeeper.KeepSerializable<T> keeps the serializers of T: MessagePack's, plus MemoryPack's ones
// (and the generic method that installs its closed formatters) when ActualLab.Serialization.MemoryPack
// is referenced. Its body never runs; the AOT compiler only sees what it references.
public static class SerializableTypeRoots
{
    public static void Keep()
    {
        CodeKeeper.KeepSerializable<SayHelloCommand>();
        CodeKeeper.KeepSerializable<ApiDto>();
        CodeKeeper.KeepSerializable<PropertyBag<CustomSchema>>();
        CodeKeeper.KeepSerializable<SanitizedString<Sanitizers.Hidden>>();
        CodeKeeper.KeepSerializable<SanitizedString<CustomSanitizer>>();
        CodeKeeper.KeepSerializable<Option<int>>();
        CodeKeeper.KeepSerializable<ApiOption<int>>();
        CodeKeeper.KeepSerializable<ApiNullable<int>>();
        CodeKeeper.KeepSerializable<ApiNullable8<long>>();
        CodeKeeper.KeepSerializable<ApiArray<int>>();
    }
}
