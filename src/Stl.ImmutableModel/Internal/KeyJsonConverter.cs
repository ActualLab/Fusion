using System;
using Newtonsoft.Json;

namespace Stl.ImmutableModel.Internal 
{
    public class KeyJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) 
            => objectType == typeof(Key);

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var key = (Key) value!;
            writer.WriteValue(key.Format());
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var value = (string) reader.Value!;
            return KeyParser.Parse(value);
        }
    }
}
