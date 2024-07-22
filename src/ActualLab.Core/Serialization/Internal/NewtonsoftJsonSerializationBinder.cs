using Newtonsoft.Json.Serialization;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Serialization.Internal;

public static class NewtonsoftJsonSerializationBinder
{
    private static ISerializationBinder? _default;

    public static ISerializationBinder Default {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _default ??= new JsonSerializer().SerializationBinder;
    }
}
