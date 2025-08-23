using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
public class SystemJsonSerializer : TextSerializerBase
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static volatile SystemJsonSerializer? _pretty;
    private static volatile SystemJsonSerializer? _default;
    private static volatile TypeDecoratingTextSerializer? _defaultTypeDecorating;

    public static JsonSerializerOptions PrettyOptions { get; set; }
        = new() { WriteIndented = true };
    public static JsonSerializerOptions DefaultOptions { get; set; }
        = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static SystemJsonSerializer Pretty {
        get {
            if (_pretty is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _pretty ??= new(PrettyOptions);
        }
        set {
            lock (StaticLock)
                _pretty = value;
        }
    }

    public static SystemJsonSerializer Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _default ??= new(DefaultOptions);
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

    public JsonSerializerOptions Options { get; }

    public SystemJsonSerializer() : this(DefaultOptions) { }
    public SystemJsonSerializer(JsonSerializerOptions options)
    {
        Options = options;
        PreferStringApi = false;
    }

    // Read

    public override object? Read(string data, Type type)
        => JsonSerializer.Deserialize(data, type, Options);
    public override object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var reader = new Utf8JsonReader(data.Span);
        var result = JsonSerializer.Deserialize(ref reader, type, Options);
        readLength = (int)reader.BytesConsumed;
        return result;
    }

    public override object? Read(ReadOnlyMemory<char> data, Type type)
        => JsonSerializer.Deserialize(data.Span, type, Options);

    // Write

    public override string Write(object? value, Type type)
        => JsonSerializer.Serialize(value, type, Options);

    public override void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        var utf8JsonWriter = new Utf8JsonWriter(bufferWriter);
        JsonSerializer.Serialize(utf8JsonWriter, value, type, Options);
    }

    public override void Write(TextWriter textWriter, object? value, Type type)
    {
        var result = JsonSerializer.Serialize(value, type, Options);
        textWriter.Write(result);
    }
}
