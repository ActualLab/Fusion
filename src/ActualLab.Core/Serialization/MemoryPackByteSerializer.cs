using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.OS;
using Errors = ActualLab.Serialization.Internal.Errors;

#if NETSTANDARD2_0
using MessagePack;
#endif

namespace ActualLab.Serialization;

public class MemoryPackByteSerializer(MemoryPackSerializerOptions options) : IByteSerializer
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private readonly ConcurrentDictionary<Type, MemoryPackByteSerializer> _typedSerializerCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static MemoryPackByteSerializer? _default;
    private static TypeDecoratingByteSerializer? _defaultTypeDecorating;

    public static MemoryPackSerializerOptions DefaultOptions { get; set; } = MemoryPackSerializerOptions.Default;

    public static MemoryPackByteSerializer Default {
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

    public MemoryPackSerializerOptions Options { get; } = options;

    public MemoryPackByteSerializer() : this(DefaultOptions) { }

    public IByteSerializer<T> ToTyped<T>(Type? serializedType = null)
        => (IByteSerializer<T>) GetTypedSerializer(serializedType ?? typeof(T));

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public virtual object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var serializer = _typedSerializerCache.GetOrAdd(type,
            static (type1, self) => (MemoryPackByteSerializer)typeof(MemoryPackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1),
            this);
        return serializer.Read(data, type, out readLength);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public virtual void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        var serializer = _typedSerializerCache.GetOrAdd(type,
            static (type1, self) => (MemoryPackByteSerializer)typeof(MemoryPackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1),
            this);
        serializer.Write(bufferWriter, value, type);
    }

    // Private methods

    private MemoryPackByteSerializer GetTypedSerializer(Type serializedType)
        => _typedSerializerCache.GetOrAdd(serializedType,
            static (type1, self) => (MemoryPackByteSerializer)typeof(MemoryPackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Options, type1),
            this);
}

public class MemoryPackByteSerializer<T>(MemoryPackSerializerOptions options, Type serializedType)
    : MemoryPackByteSerializer(options), IByteSerializer<T>
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
    {
#if !NETSTANDARD2_0
        var result = default(T);
        readLength = MemoryPackSerializer.Deserialize(data.Span, ref result, Options);
        return result!;
#else
        return MessagePackSerializer.Deserialize<T>(data, MessagePackByteSerializer.DefaultOptions, out readLength);
#endif
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public void Write(IBufferWriter<byte> bufferWriter, T value)
#if !NETSTANDARD2_0
        => MemoryPackSerializer.Serialize(bufferWriter, value, Options);
#else
        => MessagePackSerializer.Serialize(bufferWriter, value, MessagePackByteSerializer.DefaultOptions);
#endif
}
