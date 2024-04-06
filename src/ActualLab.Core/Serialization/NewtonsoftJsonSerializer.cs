using System.Diagnostics.CodeAnalysis;
using Cysharp.Text;
using Microsoft.Toolkit.HighPerformance;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ActualLab.Internal;
using ActualLab.Serialization.Internal;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Serialization;

#pragma warning disable CA2326, CA2327, CA2328, IL2116

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Serialization)]
#endif
public class NewtonsoftJsonSerializer : TextSerializerBase
{
    private readonly JsonSerializer _jsonSerializer;
    private static NewtonsoftJsonSerializer? _default;
    private static TypeDecoratingTextSerializer? _defaultTypeDecorating;

    public static JsonSerializerSettings DefaultSettings { get; set; } = new() {
#if !NET5_0_OR_GREATER
        SerializationBinder = CrossPlatformSerializationBinder.Instance,
#endif
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
        DateParseHandling = DateParseHandling.None, // This makes sure all strings are deserialized as-is
        ContractResolver = new DefaultContractResolver(),
    };

    public static NewtonsoftJsonSerializer Default {
        get => _default ??= new(DefaultSettings);
        set => _default = value;
    }

    public static TypeDecoratingTextSerializer DefaultTypeDecorating {
        [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
        get => _defaultTypeDecorating ??= new(Default);
        set => _defaultTypeDecorating = value;
    }

    // Instance members

    public JsonSerializerSettings Settings { get; }

    public NewtonsoftJsonSerializer(JsonSerializerSettings? settings = null)
    {
        Settings = settings ??= DefaultSettings;
        _jsonSerializer = JsonSerializer.Create(settings);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    static NewtonsoftJsonSerializer()
    { }

    // Read

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override object? Read(string data, Type type)
        => _jsonSerializer.Deserialize(new StringReader(data), type);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        using var stream = data.AsStream();
        using var reader = new StreamReader(stream);
        var result = _jsonSerializer.Deserialize(reader, type);
        readLength = (int)stream.Position;
        return result;
    }

    // Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override string Write(object? value, Type type)
    {
        using var stringWriter = new ZStringWriter();
        using var writer = new JsonTextWriter(stringWriter);
        writer.Formatting = _jsonSerializer.Formatting;
        // ReSharper disable once HeapView.BoxingAllocation
        _jsonSerializer.Serialize(writer, value, type);
        return stringWriter.ToString();
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(TextWriter textWriter, object? value, Type type)
    {
        using var writer = new JsonTextWriter(textWriter);
        writer.Formatting = _jsonSerializer.Formatting;
        // ReSharper disable once HeapView.BoxingAllocation
        _jsonSerializer.Serialize(writer, value, type);
    }
}
