using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization.Internal;

[method: JsonConstructor]
public sealed record JsonRpcMessage(
    byte CallTypeId,
    long RelatedId,
    string? Method,
    List<string>? Headers)
{
    private const int HeaderBufferCapacity = 16;
    private const int HeaderBufferReplaceCapacity = 256;
    [ThreadStatic] private static List<RpcHeader>? _headerBuffer;

    public JsonRpcMessage(RpcMessage source)
        : this(source.CallTypeId, source.RelatedId, source.MethodRef.Target!.FullName, FormatHeaders(source.Headers))
    { }

    public static List<string>? FormatHeaders(RpcHeader[]? headers)
    {
        if (headers == null || headers.Length == 0)
            return null;

        var result = new List<string>();
        foreach (var header in headers) {
            result.Add(header.Key.Name.Value);
            result.Add(header.Value);
        }

        return result;
    }

    public RpcHeader[]? ParseHeaders()
    {
        if (Headers == null || Headers.Count == 0)
            return null;

        var buffer = _headerBuffer ??= new List<RpcHeader>(HeaderBufferCapacity);
        if (buffer.Capacity > HeaderBufferReplaceCapacity)
            buffer = _headerBuffer = new List<RpcHeader>(HeaderBufferCapacity);
        else
            buffer.Clear();
        for (var i = 0; i < Headers.Count; i += 2) {
            var key = RpcHeaderKey.NewOrWellKnown(Headers[i]);
            buffer.Add(new RpcHeader(key, Headers[i + 1]));
        }
        return buffer.ToArray();
    }
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(JsonRpcMessage))]
internal partial class JsonRpcMessageContext : JsonSerializerContext;
