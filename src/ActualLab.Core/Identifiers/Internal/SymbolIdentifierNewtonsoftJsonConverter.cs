using Newtonsoft.Json;

namespace ActualLab.Identifiers.Internal;

public class SymbolIdentifierNewtonsoftJsonConverter<T> : Newtonsoft.Json.JsonConverter<T>
    where T : struct, ISymbolIdentifier<T>
{
    public override void WriteJson(
        JsonWriter writer, T value,
        Newtonsoft.Json.JsonSerializer serializer)
        => writer.WriteValue(value.Value);

    public override T ReadJson(
        JsonReader reader, Type objectType, T existingValue, bool hasExistingValue,
        Newtonsoft.Json.JsonSerializer serializer)
        => SymbolIdentifier.Parse<T>((string?)reader.Value);
}
