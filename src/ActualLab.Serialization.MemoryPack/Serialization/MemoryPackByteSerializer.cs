using System.Buffers;
using ActualLab.OS;

namespace ActualLab.Serialization;

/// <summary>
/// An <see cref="IByteSerializer"/> implementation backed by MemoryPack.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
public class MemoryPackByteSerializer : IByteSerializer
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static MemoryPackByteSerializer? _default;
    private static TypeDecoratingByteSerializer? _defaultTypeDecorating;

    private readonly ConcurrentDictionary<Type, MemoryPackByteSerializer> _typedSerializerCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static MemoryPackSerializerOptions DefaultOptions { get; set; } = MemoryPackSerializerOptions.Default;

    public static MemoryPackByteSerializer Default {
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

    public MemoryPackSerializerOptions Options { get; }
    // The serializer typed serializers expose as ByteSerializer.Current:
    // their untyped (root) serializer, since only it can (de)serialize any type
    public IByteSerializer Root { get; protected set; }

    // Using the serializer must be enough to get the formatters of ActualLab types registered,
    // even on runtimes running no module initializers
    static MemoryPackByteSerializer()
        => MemoryPackSerialization.Register();

    public MemoryPackByteSerializer() : this(DefaultOptions) { }

    public MemoryPackByteSerializer(MemoryPackSerializerOptions options)
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

    private MemoryPackByteSerializer GetTypedSerializer(Type serializedType)
        => _typedSerializerCache.GetOrAdd(serializedType,
            static (type1, self) => (MemoryPackByteSerializer)typeof(MemoryPackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1, self.Root),
            this);
}

/// <summary>
/// A typed <see cref="MemoryPackByteSerializer"/> that serializes values of type <typeparamref name="T"/>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
public class MemoryPackByteSerializer<T> : MemoryPackByteSerializer, IByteSerializer<T>
{
    public Type SerializedType { get; }

    public MemoryPackByteSerializer(
        MemoryPackSerializerOptions options,
        Type serializedType,
        IByteSerializer? root = null)
        : base(options)
    {
        SerializedType = serializedType;
        if (root is not null)
            Root = root;
        MemoryPackSerialization.Register<T>();
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
    {
        var result = default(T);
        readLength = MemoryPackSerializer.Deserialize(data.Span, ref result, Options);
        return result!;
    }

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
        => MemoryPackSerializer.Serialize(bufferWriter, value, Options);
}
