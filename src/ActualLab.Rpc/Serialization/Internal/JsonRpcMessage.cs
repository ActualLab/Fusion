using System.ComponentModel;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Serialization.Internal;

[method: JsonConstructor]
public sealed record JsonRpcMessage(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault), DefaultValue((byte)0)]
    byte CallType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault), DefaultValue((long)0)]
    long RelatedId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault), DefaultValue(null)]
    string? Method,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault), DefaultValue(null)]
    List<string>? Headers)
{
    private const int HeaderBufferCapacity = 16;
    private const int HeaderBufferReplaceCapacity = 256;
    [ThreadStatic] private static List<RpcHeader>? _headerBuffer;

    public JsonRpcMessage(RpcOutboundMessage source)
        : this(source.MethodDef.CallType.Id, source.RelatedId, source.MethodDef.Ref.Target!.FullName, FormatHeaders(source.Headers))
    { }

    public static List<string>? FormatHeaders(RpcHeader[]? headers)
    {
        if (headers is null || headers.Length == 0)
            return null;

        var result = new List<string>();
        foreach (var header in headers) {
            result.Add(header.Key.Name);
            result.Add(header.Value);
        }

        return result;
    }

    public RpcHeader[]? ParseHeaders()
    {
        if (Headers is null || Headers.Count == 0)
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
internal sealed partial class JsonRpcMessageContext : JsonSerializerContext;
