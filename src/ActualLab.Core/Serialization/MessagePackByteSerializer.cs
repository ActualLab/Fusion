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
    private static readonly Lock StaticLock = LockFactory.Create();
    private readonly ConcurrentDictionary<Type, MessagePackByteSerializer> _typedSerializerCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static IFormatterResolver DefaultResolver { get; set; } = DefaultMessagePackResolver.Instance;

    [field: AllowNull, MaybeNull]
    public static MessagePackSerializerOptions DefaultOptions {
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new(DefaultResolver);
        }
        set {
            lock (StaticLock)
                field = value;
        }
    }

    [field: AllowNull, MaybeNull]
    public static MessagePackByteSerializer Default {
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
    public static TypeDecoratingByteSerializer DefaultTypeDecorating {
        get {
            if (field is { } value)
                return value;
            lock (StaticLock)
                return field ??= new TypeDecoratingByteSerializer(Default);
        }
        set {
            lock (StaticLock)
                field = value;
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
