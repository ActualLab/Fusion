using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Api.Internal;

public class ApiArrayMessagePackFormatter<T> : IMessagePackFormatter<ApiArray<T>>
{
    public void Serialize(ref MessagePackWriter writer, ApiArray<T> value, MessagePackSerializerOptions options)
    {
        if (value.IsEmpty) {
            writer.WriteArrayHeader(0);
            return;
        }

        var formatter = options.Resolver.GetFormatterWithVerify<T>();
        writer.WriteArrayHeader(value.Count);
        foreach (var item in value.Items)
            formatter.Serialize(ref writer, item, options);
    }

    public ApiArray<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var len = reader.ReadArrayHeader();
        if (len == 0)
            return ApiArray<T>.Empty;

        var formatter = options.Resolver.GetFormatterWithVerify<T>();
        var array = new T[len];
        options.Security.DepthStep(ref reader);
        try {
            for (int i = 0; i < len; i++)
                array[i] = formatter.Deserialize(ref reader, options);
        }
        finally {
            reader.Depth--;
        }
        return new ApiArray<T>(array);
    }
}
