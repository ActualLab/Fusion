using System.Buffers;
using ActualLab.Fusion.Internal;
using ActualLab.OS;
using ActualLab.Serialization.Internal;
using Nerdbank.MessagePack;
using PolyType;
using PolyType.ReflectionProvider;
using Errors = ActualLab.Serialization.Internal.Errors;
using NerdbankSerializer = Nerdbank.MessagePack.MessagePackSerializer;

namespace ActualLab.Serialization;

/// <summary>
/// An <see cref="IByteSerializer"/> implementation backed by Nerdbank.MessagePack.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume serializable types are fully preserved")]
public class NerdbankMessagePackByteSerializer(NerdbankSerializer serializer, ITypeShapeProvider typeShapeProvider)
    : IByteSerializer
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static volatile NerdbankSerializer? _defaultSerializer;
    private static volatile NerdbankMessagePackByteSerializer? _default;
    private static volatile TypeDecoratingByteSerializer? _defaultTypeDecorating;

    private readonly ConcurrentDictionary<Type, NerdbankMessagePackByteSerializer> _typedSerializerCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static ITypeShapeProvider DefaultTypeShapeProvider { get; set; }
        = ReflectionTypeShapeProvider.Default;

    public static NerdbankSerializer DefaultSerializer {
        get {
            if (_defaultSerializer is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _defaultSerializer ??= CreateDefaultSerializer();
        }
        set {
            lock (StaticLock)
                _defaultSerializer = value;
        }
    }

    public static NerdbankMessagePackByteSerializer Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock)
                // ReSharper disable once NonAtomicCompoundOperator
                return _default ??= new(DefaultSerializer, DefaultTypeShapeProvider);
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
                // ReSharper disable once NonAtomicCompoundOperator
                return _defaultTypeDecorating ??= new TypeDecoratingByteSerializer(Default);
        }
        set {
            lock (StaticLock)
                _defaultTypeDecorating = value;
        }
    }

    // Instance members

    public NerdbankSerializer Serializer { get; } = serializer;
    public ITypeShapeProvider TypeShapeProvider { get; } = typeShapeProvider;

    public NerdbankMessagePackByteSerializer()
        : this(DefaultSerializer, DefaultTypeShapeProvider)
    { }

    public IByteSerializer<T> ToTyped<T>(Type? serializedType = null)
        => (IByteSerializer<T>)GetTypedSerializer(serializedType ?? typeof(T));

    public virtual object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        var serializer = GetTypedSerializer(type);
        return serializer.Read(data, type, out readLength);
    }

    public virtual void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        var serializer = GetTypedSerializer(type);
        serializer.Write(bufferWriter, value, type);
    }

    // Private methods

    private NerdbankMessagePackByteSerializer GetTypedSerializer(Type serializedType)
        => _typedSerializerCache.GetOrAdd(serializedType,
            static (type1, self) => (NerdbankMessagePackByteSerializer)typeof(NerdbankMessagePackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Serializer, self.TypeShapeProvider, type1),
            this);

    private static NerdbankSerializer CreateDefaultSerializer()
        => new() {
            Converters = [
                new UnitNerdbankConverter(),
                new MomentNerdbankConverter(),
                new CpuTimestampNerdbankConverter(),
                new SymbolNerdbankConverter(),
                new HostIdNerdbankConverter(),
                new FilePathNerdbankConverter(),
                new TypeRefNerdbankConverter(),
                new JsonStringNerdbankConverter(),
                new ByteStringNerdbankConverter(),
                new MessagePackDataNerdbankConverter(),
                new SessionNerdbankConverter(),
            ],
            ConverterTypes = [
                typeof(OptionNerdbankConverter<>),
                typeof(ApiOptionNerdbankConverter<>),
                typeof(ApiNullableNerdbankConverter<>),
                typeof(ApiNullable8NerdbankConverter<>),
                typeof(ApiArrayNerdbankConverter<>),
                typeof(RpcStreamNerdbankConverter<>),
            ],
        };
}

/// <summary>
/// A typed <see cref="NerdbankMessagePackByteSerializer"/> that serializes values of type <typeparamref name="T"/>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume serializable types are fully preserved")]
public class NerdbankMessagePackByteSerializer<T>(
    NerdbankSerializer serializer,
    ITypeShapeProvider typeShapeProvider,
    Type serializedType
) : NerdbankMessagePackByteSerializer(serializer, typeShapeProvider), IByteSerializer<T>
{
    private readonly ITypeShape<T> _shape = (ITypeShape<T>)typeShapeProvider.GetTypeShape(serializedType)!;

    public Type SerializedType { get; } = serializedType;

    public override object? Read(ReadOnlyMemory<byte> data, Type type, out int readLength)
    {
        if (type != SerializedType)
            throw Errors.SerializedTypeMismatch(SerializedType, type);

        // ReSharper disable once HeapView.PossibleBoxingAllocation
        return Read(data, out readLength);
    }

    public override void Write(IBufferWriter<byte> bufferWriter, object? value, Type type)
    {
        if (type != SerializedType)
            throw Errors.SerializedTypeMismatch(SerializedType, type);

        Write(bufferWriter, (T)value!);
    }

    public T Read(ReadOnlyMemory<byte> data, out int readLength)
    {
        var sequence = new ReadOnlySequence<byte>(data);
        var reader = new MessagePackReader(sequence);
        var result = Serializer.Deserialize<T>(ref reader, _shape);
        readLength = (int)reader.Consumed;
        return result;
    }

    public void Write(IBufferWriter<byte> bufferWriter, T value)
        => Serializer.Serialize(bufferWriter, value, _shape);
}
