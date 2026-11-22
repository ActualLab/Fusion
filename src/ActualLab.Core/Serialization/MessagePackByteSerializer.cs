using System.Buffers;
using MessagePack;
using ActualLab.OS;
using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

/// <summary>
/// An <see cref="IByteSerializer"/> implementation backed by MessagePack.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
public class MessagePackByteSerializer : IByteSerializer
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static MessagePackSerializerOptions? _defaultOptions;
    private static MessagePackByteSerializer? _default;
    private static TypeDecoratingByteSerializer? _defaultTypeDecorating;

    private readonly ConcurrentDictionary<Type, MessagePackByteSerializer> _typedSerializerCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static IFormatterResolver DefaultResolver { get; set; } = DefaultMessagePackResolver.Instance;

    public static MessagePackSerializerOptions DefaultOptions {
        get {
            if (_defaultOptions is { } value)
                return value;
            lock (StaticLock) {
                if (_defaultOptions is { } newValue)
                    return newValue;

                newValue = new(DefaultResolver);
                Volatile.Write(ref _defaultOptions, newValue);
                return newValue;
            }
        }
        set {
            lock (StaticLock)
                Volatile.Write(ref _defaultOptions, value);
        }
    }

    public static MessagePackByteSerializer Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock) {
                if (_default is { } newValue)
                    return newValue;

                newValue = new(DefaultOptions);
                Volatile.Write(ref _default, newValue);
                return newValue;
            }
        }
        set {
            lock (StaticLock)
                Volatile.Write(ref _default, value);
        }
    }

    public static TypeDecoratingByteSerializer DefaultTypeDecorating {
        get {
            if (_defaultTypeDecorating is { } value)
                return value;
            lock (StaticLock) {
                if (_defaultTypeDecorating is { } newValue)
                    return newValue;

                newValue = new TypeDecoratingByteSerializer(Default);
                Volatile.Write(ref _defaultTypeDecorating, newValue);
                return newValue;
            }
        }
        set {
            lock (StaticLock)
                Volatile.Write(ref _defaultTypeDecorating, value);
        }
    }

    // Instance members

    public MessagePackSerializerOptions Options { get; }
    // The serializer typed serializers expose as ByteSerializer.Current:
    // their untyped (root) serializer, since only it can (de)serialize any type
    public IByteSerializer Root { get; protected set; }

    public MessagePackByteSerializer() : this(DefaultOptions) { }

    public MessagePackByteSerializer(MessagePackSerializerOptions options)
    {
        Options = options;
        Root = this;
    }

    public IByteSerializer<T> ToTyped<T>(Type? serializedType = null)
        => (IByteSerializer<T>)GetTypedSerializer(serializedType ?? typeof(T));

    // Unscoped API

    public virtual object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength)
        => GetTypedSerializer(type).ReadUnscoped(data, type, out readLength);

    public virtual void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type)
        => GetTypedSerializer(type).WriteUnscoped(bufferWriter, value, type);

    // Private methods

    private MessagePackByteSerializer GetTypedSerializer(Type serializedType)
        => _typedSerializerCache.GetOrAdd(serializedType,
            static (type1, self) => (MessagePackByteSerializer)typeof(MessagePackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1, self.Root),
            this);
}

/// <summary>
/// A typed <see cref="MessagePackByteSerializer"/> that serializes values of type <typeparamref name="T"/>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
public class MessagePackByteSerializer<T> : MessagePackByteSerializer, IByteSerializer<T>
{
    public Type SerializedType { get; }

    public MessagePackByteSerializer(
        MessagePackSerializerOptions options,
        Type serializedType,
        IByteSerializer? root = null)
        : base(options)
    {
        SerializedType = serializedType;
        if (root is not null)
            Root = root;
    }

    // Unscoped API

    public override object? ReadUnscoped(ReadOnlyMemory<byte> data, Type type, out int readLength)
        // ReSharper disable once HeapView.PossibleBoxingAllocation
        => type == SerializedType
            ? ReadUnscoped(data, out readLength)
            : base.ReadUnscoped(data, type, out readLength);

    public override void WriteUnscoped(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        if (type == SerializedType)
            WriteUnscoped(bufferWriter, (T)value!);
        else
            base.WriteUnscoped(bufferWriter, value, type);
    }

    public T ReadUnscoped(ReadOnlyMemory<byte> data, out int readLength)
        => MessagePackSerializer.Deserialize<T>(data, Options, out readLength);

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
        => MessagePackSerializer.Serialize(bufferWriter, value, Options);
}
