using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using MessagePack;
using ActualLab.Internal;
using ActualLab.OS;
using ActualLab.Serialization.Internal;
using Errors = ActualLab.Serialization.Internal.Errors;

namespace ActualLab.Serialization;

public class MessagePackByteSerializer(MessagePackSerializerOptions options) : IByteSerializer
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private readonly ConcurrentDictionary<Type, MessagePackByteSerializer> _typedSerializerCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static MessagePackSerializerOptions? _defaultOptions;
    private static MessagePackByteSerializer? _default;
    private static TypeDecoratingByteSerializer? _defaultTypeDecorating;

    public static IFormatterResolver DefaultResolver { get; set; } = DefaultMessagePackResolver.Instance;

    public static MessagePackSerializerOptions DefaultOptions {
        get {
            if (_defaultOptions is { } value)
                return value;
            lock (StaticLock)
                return _defaultOptions ??= new(DefaultResolver);
        }
        set {
            lock (StaticLock)
                _defaultOptions = value;
        }
    }

    public static MessagePackByteSerializer Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock)
                return _default ??= new(DefaultOptions);
        }
        set {
            lock (StaticLock)
                _default = value;
        }
    }

    public static TypeDecoratingByteSerializer DefaultTypeDecorating {
        get {
            if (_defaultTypeDecorating is { } value)
                return value;
            lock (StaticLock)
                return _defaultTypeDecorating ??= new TypeDecoratingByteSerializer(Default);
        }
        set {
            lock (StaticLock)
                _defaultTypeDecorating = value;
        }
    }

    // Instance members

    public MessagePackSerializerOptions Options { get; } = options;

    public MessagePackByteSerializer() : this(DefaultOptions) { }

    public IByteSerializer<T> ToTyped<T>(Type? serializedType = null)
        => (IByteSerializer<T>) GetTypedSerializer(serializedType ?? typeof(T));

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public virtual object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var serializer = _typedSerializerCache.GetOrAdd(type,
            static (type1, self) => (MessagePackByteSerializer)typeof(MessagePackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1),
            this);
        return serializer.Read(data, type, out readLength);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public virtual void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        var serializer = _typedSerializerCache.GetOrAdd(type,
            static (type1, self) => (MessagePackByteSerializer)typeof(MessagePackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1),
            this);
        serializer.Write(bufferWriter, value, type);
    }

    // Private methods

    private MessagePackByteSerializer GetTypedSerializer(Type serializedType)
        => _typedSerializerCache.GetOrAdd(serializedType,
            static (type1, self) => (MessagePackByteSerializer)typeof(MessagePackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1),
            this);
}

public class MessagePackByteSerializer<T>(MessagePackSerializerOptions options, Type serializedType)
    : MessagePackByteSerializer(options), IByteSerializer<T>
{
    public Type SerializedType { get; } = serializedType;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        if (type != SerializedType)
            throw Errors.SerializedTypeMismatch(SerializedType, type);

        // ReSharper disable once HeapView.PossibleBoxingAllocation
        return Read(data, out readLength);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        if (type != SerializedType)
            throw Errors.SerializedTypeMismatch(SerializedType, type);

        Write(bufferWriter, (T)value!);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public T Read(ReadOnlyMemory<byte> data, out int readLength)
        => MessagePackSerializer.Deserialize<T>(data, Options, out readLength);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public void Write(IBufferWriter<byte> bufferWriter, T value)
        => MessagePackSerializer.Serialize(bufferWriter, value, Options);
}
