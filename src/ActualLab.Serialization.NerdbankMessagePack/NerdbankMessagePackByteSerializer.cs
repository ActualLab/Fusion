using System.Buffers;
using ActualLab.Fusion.Internal;
using ActualLab.OS;
using ActualLab.Serialization.Internal;
using Nerdbank.MessagePack;
using PolyType;
using PolyType.ReflectionProvider;
using NerdbankSerializer = Nerdbank.MessagePack.MessagePackSerializer;

namespace ActualLab.Serialization;

/// <summary>
/// An <see cref="IByteSerializer"/> implementation backed by Nerdbank.MessagePack.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume serializable types are fully preserved")]
public class NerdbankMessagePackByteSerializer : IByteSerializer
{
    // Nerdbank.MessagePack 1.0.x has no SecuritySettings.MaxCollectionPreallocation, so converters
    // reading a wire-supplied collection header cap the initial buffer here and grow it as the items
    // are actually read - a declared count alone must never size an allocation.
    public const int MaxCollectionPreallocation = 4096;

#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif
    private static NerdbankSerializer? _defaultSerializer;
    private static NerdbankMessagePackByteSerializer? _default;
    private static TypeDecoratingByteSerializer? _defaultTypeDecorating;

    private readonly ConcurrentDictionary<Type, NerdbankMessagePackByteSerializer> _typedSerializerCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static ITypeShapeProvider DefaultTypeShapeProvider { get; set; }
        = ReflectionTypeShapeProvider.Default;

    public static NerdbankSerializer DefaultSerializer {
        get {
            if (_defaultSerializer is { } value)
                return value;
            lock (StaticLock) {
                if (_defaultSerializer is { } newValue)
                    return newValue;

                newValue = CreateDefaultSerializer();
                Volatile.Write(ref _defaultSerializer, newValue);
                return newValue;
            }
        }
        set {
            lock (StaticLock)
                Volatile.Write(ref _defaultSerializer, value);
        }
    }

    public static NerdbankMessagePackByteSerializer Default {
        get {
            if (_default is { } value)
                return value;
            lock (StaticLock) {
                if (_default is { } newValue)
                    return newValue;

                newValue = new(DefaultSerializer, DefaultTypeShapeProvider);
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

    static NerdbankMessagePackByteSerializer()
        => NerdbankMessagePackSerialization.Register();

    // Instance members

    public NerdbankSerializer Serializer { get; }
    public ITypeShapeProvider TypeShapeProvider { get; }
    // The serializer typed serializers expose as ByteSerializer.Current:
    // their untyped (root) serializer, since only it can (de)serialize any type
    public IByteSerializer Root { get; protected set; }

    public NerdbankMessagePackByteSerializer()
        : this(DefaultSerializer, DefaultTypeShapeProvider)
    { }

    public NerdbankMessagePackByteSerializer(NerdbankSerializer serializer, ITypeShapeProvider typeShapeProvider)
    {
        Serializer = serializer;
        TypeShapeProvider = typeShapeProvider;
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

    private NerdbankMessagePackByteSerializer GetTypedSerializer(Type serializedType)
        => _typedSerializerCache.GetOrAdd(serializedType,
            static (type1, self) => (NerdbankMessagePackByteSerializer)typeof(NerdbankMessagePackByteSerializer<>)
                .MakeGenericType(type1)
                .CreateInstance(self.Serializer, self.TypeShapeProvider, type1, self.Root),
            this);

    private static NerdbankSerializer CreateDefaultSerializer()
    {
        var serializer = new NerdbankSerializer {
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
                new RpcHandshakeNerdbankConverter(),
                new VersionSetNerdbankConverter(),
                new RpcObjectIdNerdbankConverter(),
                new RpcHeaderNerdbankConverter(),
                new RpcHeaderKeyNerdbankConverter(),
                new RpcMethodRefNerdbankConverter(),
                new RpcCacheKeyNerdbankConverter(),
                new RpcCacheValueNerdbankConverter(),
            ],
            ConverterFactories = [new NewtonsoftJsonSerializedNerdbankConverterFactory()],
            ConverterTypes = [
                typeof(OptionNerdbankConverter<>),
                typeof(ResultNerdbankConverter<>),
                typeof(ApiOptionNerdbankConverter<>),
                typeof(ApiNullableNerdbankConverter<>),
                typeof(ApiNullable8NerdbankConverter<>),
                typeof(ApiArrayNerdbankConverter<>),
                typeof(ApiMapNerdbankConverter<,>),
                typeof(RpcStreamNerdbankConverter<>),
                typeof(UniSerializedNerdbankConverter<>),
                typeof(TypeDecoratingUniSerializedNerdbankConverter<,>),
                typeof(PropertyBagNerdbankConverter<>),
                typeof(PropertyBagItemNerdbankConverter<>),
                typeof(SanitizedStringNerdbankConverter<>),
            ],
        };
        // Encode Guid as 36-char ASCII string ("d9 24 ..."), matching MessagePack-CSharp's default.
        // Required for wire compatibility with the TS RPC client (which sends GUIDs as strings)
        // and any legacy .NET data written by the MessagePack-CSharp serializer.
        return serializer.WithGuidConverter(OptionalConverters.GuidStringFormat.StringD);
    }
}

/// <summary>
/// A typed <see cref="NerdbankMessagePackByteSerializer"/> that serializes values of type <typeparamref name="T"/>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume serializable types are fully preserved")]
public class NerdbankMessagePackByteSerializer<T> : NerdbankMessagePackByteSerializer, IByteSerializer<T>
{
    private readonly ITypeShape<T> _shape;

    public Type SerializedType { get; }

    public NerdbankMessagePackByteSerializer(
        NerdbankSerializer serializer,
        ITypeShapeProvider typeShapeProvider,
        Type serializedType,
        IByteSerializer? root = null)
        : base(serializer, typeShapeProvider)
    {
        SerializedType = serializedType;
        _shape = (ITypeShape<T>)typeShapeProvider.GetTypeShape(serializedType)!;
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
    {
        var sequence = new ReadOnlySequence<byte>(data);
        var reader = new MessagePackReader(sequence);
        var result = Serializer.Deserialize<T>(ref reader, _shape);
        readLength = (int)reader.Consumed;
        return result!;
    }

    public void WriteUnscoped(IBufferWriter<byte> bufferWriter, T value)
        => Serializer.Serialize(bufferWriter, value, _shape);
}
