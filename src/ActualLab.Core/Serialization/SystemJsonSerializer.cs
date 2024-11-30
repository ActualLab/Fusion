using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

#pragma warning disable IL2026

public class SystemJsonSerializer : TextSerializerBase
{
    private static readonly Lock StaticLock = LockFactory.Create();

    public static JsonSerializerOptions PrettyOptions { get; set; }
        = new() { WriteIndented = true };
    public static JsonSerializerOptions DefaultOptions { get; set; }
        = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [field: AllowNull, MaybeNull]
    public static SystemJsonSerializer Pretty {
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new(PrettyOptions);
        }
        set {
            lock (StaticLock)
                field = value;
        }
    }

    [field: AllowNull, MaybeNull]
    public static SystemJsonSerializer Default {
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new(DefaultOptions);
        }
        set {
            lock (StaticLock)
                field = value;
        }
    }

    [field: AllowNull, MaybeNull]
    public static TypeDecoratingTextSerializer DefaultTypeDecorating {
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new TypeDecoratingTextSerializer(Default);
        }
        set {
            lock (StaticLock)
                field = value;
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

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override object? Read(string data, Type type)
        => JsonSerializer.Deserialize(data, type, Options);
    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var reader = new Utf8JsonReader(data.Span);
        var result = JsonSerializer.Deserialize(ref reader, type, Options);
        readLength = (int)reader.BytesConsumed;
        return result;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override object? Read(ReadOnlyMemory<char> data, Type type)
        => JsonSerializer.Deserialize(data.Span, type, Options);

    // Write

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override string Write(object? value, Type type)
        => JsonSerializer.Serialize(value, type, Options);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        var utf8JsonWriter = new Utf8JsonWriter(bufferWriter);
        JsonSerializer.Serialize(utf8JsonWriter, value, type, Options);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(TextWriter textWriter, object? value, Type type)
    {
        var result = JsonSerializer.Serialize(value, type, Options);
        textWriter.Write(result);
    }
}
