using System.Diagnostics.CodeAnalysis;
using Cysharp.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ActualLab.Serialization.Internal;
using CommunityToolkit.HighPerformance;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ActualLab.Serialization;

#pragma warning disable CA2326, CA2327, CA2328, IL2116

public class NewtonsoftJsonSerializer : TextSerializerBase
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private readonly JsonSerializer _jsonSerializer;
    private static volatile NewtonsoftJsonSerializer? _default;
    private static volatile TypeDecoratingTextSerializer? _defaultTypeDecorating;

    public static JsonSerializerSettings DefaultSettings { get; set; } = new() {
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
        DateParseHandling = DateParseHandling.None, // This makes sure all strings are deserialized as-is
        ContractResolver = new DefaultContractResolver(),
    };

    public static NewtonsoftJsonSerializer Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _default ??= new(DefaultSettings);
        }
        set {
            lock (StaticLock)
                _default = value;
        }
    }

    public static TypeDecoratingTextSerializer DefaultTypeDecorating {
        get {
            if (_defaultTypeDecorating is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _defaultTypeDecorating ??= new TypeDecoratingTextSerializer(Default);
        }
        set {
            lock (StaticLock)
                _defaultTypeDecorating = value;
        }
    }

    // Instance members

    public JsonSerializerSettings Settings { get; }

    public NewtonsoftJsonSerializer(JsonSerializerSettings? settings = null)
    {
        Settings = settings ??= DefaultSettings;
        _jsonSerializer = JsonSerializer.Create(settings);
    }

    // Read

    public override object? Read(string data, Type type)
    {
        var stringReader = new StringReader(data); // No need to dispose
        var reader = new JsonTextReader(stringReader) { CloseInput = false }; // No need to dispose
        return _jsonSerializer.Deserialize(reader, type);
    }

    public override object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var streamReader = new StreamReader(data.AsStream()); // No need to dispose
        var reader = new JsonTextReader(streamReader) { CloseInput = false }; // No need to dispose
        var result = _jsonSerializer.Deserialize(reader, type);
        readLength = data.Length; // Always full length!
        return result;
    }

    // Write

    public override string Write(object? value, Type type)
    {
        using var stringWriter = new ZStringWriter();
        using var writer = new JsonTextWriter(stringWriter) { CloseOutput = false };
        writer.Formatting = _jsonSerializer.Formatting;
        // ReSharper disable once HeapView.BoxingAllocation
        _jsonSerializer.Serialize(writer, value, type);
        return stringWriter.ToString();
    }

    public override void Write(TextWriter textWriter, object? value, Type type)
    {
        using var writer = new JsonTextWriter(textWriter) { CloseOutput = false };
        writer.Formatting = _jsonSerializer.Formatting;
        // ReSharper disable once HeapView.BoxingAllocation
        _jsonSerializer.Serialize(writer, value, type);
    }
}
