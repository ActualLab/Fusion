using ActualLab.Collections.Internal;
using ActualLab.CommandR.Operations;
using ActualLab.Compliance;
using ActualLab.Compliance.Internal;
using ActualLab.Fusion.Authentication;
using ActualLab.Fusion.Extensions;
using ActualLab.IO;
using ActualLab.Rpc;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Serialization.Internal;

namespace ActualLab.Serialization;

/// <summary>
/// Registers the MemoryPack formatters of ActualLab types. They produce exactly the bytes
/// the MemoryPack generator produced for these types when they were <c>[MemoryPackable]</c>.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume serializable types are fully preserved")]
public static class MemoryPackSerialization
{
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif
    private static readonly ConcurrentDictionary<Type, Unit> RegisteredTypes = new();
    private static readonly MethodInfo RegisterClosedFormatterMethod = typeof(MemoryPackSerialization)
        .GetMethod(nameof(RegisterClosedFormatter), BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly Assembly MemoryPackCoreAssembly = typeof(MemoryPackFormatterProvider).Assembly;
    // Generic type definition -> its formatter's generic type definition
    private static readonly Dictionary<Type, Type> GenericFormatterTypes = new() {
        [typeof(Option<>)] = FormatterType(typeof(OptionMemoryPackFormatter<>)),
        [typeof(ApiOption<>)] = FormatterType(typeof(ApiOptionMemoryPackFormatter<>)),
        [typeof(ApiNullable<>)] = FormatterType(typeof(ApiNullableMemoryPackFormatter<>)),
        [typeof(ApiNullable8<>)] = FormatterType(typeof(ApiNullable8MemoryPackFormatter<>)),
        [typeof(ApiArray<>)] = FormatterType(typeof(ApiArrayMemoryPackFormatter<>)),
        [typeof(ApiList<>)] = FormatterType(typeof(ApiListMemoryPackFormatter<>)),
        [typeof(ApiSet<>)] = FormatterType(typeof(ApiSetMemoryPackFormatter<>)),
        [typeof(ApiMap<,>)] = FormatterType(typeof(ApiMapMemoryPackFormatter<,>)),
        [typeof(Result<>)] = FormatterType(typeof(ResultMemoryPackFormatter<>)),
        [typeof(Box<>)] = FormatterType(typeof(BoxMemoryPackFormatter<>)),
        [typeof(MutableBox<>)] = FormatterType(typeof(MutableBoxMemoryPackFormatter<>)),
        [typeof(ImmutableBimap<,>)] = FormatterType(typeof(ImmutableBimapMemoryPackFormatter<,>)),
        [typeof(PropertyBag<>)] = FormatterType(typeof(PropertyBagMemoryPackFormatter<>)),
        [typeof(MutablePropertyBag<>)] = FormatterType(typeof(MutablePropertyBagMemoryPackFormatter<>)),
        [typeof(PropertyBagItem<>)] = FormatterType(typeof(PropertyBagItemMemoryPackFormatter<>)),
        [typeof(SanitizedString<>)] = FormatterType(typeof(SanitizedStringMemoryPackFormatter<>)),
        [typeof(UniSerialized<>)] = FormatterType(typeof(UniSerializedMemoryPackFormatter<>)),
        [typeof(TypeDecoratingUniSerialized<,>)]
            = FormatterType(typeof(TypeDecoratingUniSerializedMemoryPackFormatter<,>)),
        [typeof(ByteSerialized<>)] = FormatterType(typeof(ByteSerializedMemoryPackFormatter<>)),
        [typeof(MemoryPackSerialized<>)] = FormatterType(typeof(MemoryPackSerializedMemoryPackFormatter<>)),
        [typeof(MessagePackSerialized<>)] = FormatterType(typeof(MessagePackSerializedMemoryPackFormatter<>)),
        [typeof(TypeDecoratingMemoryPackSerialized<,>)]
            = FormatterType(typeof(TypeDecoratingMemoryPackSerializedMemoryPackFormatter<,>)),
        [typeof(TypeDecoratingMessagePackSerialized<,>)]
            = FormatterType(typeof(TypeDecoratingMessagePackSerializedMemoryPackFormatter<,>)),
        [typeof(TextSerialized<>)] = FormatterType(typeof(TextSerializedMemoryPackFormatter<>)),
        [typeof(SystemJsonSerialized<>)] = FormatterType(typeof(SystemJsonSerializedMemoryPackFormatter<>)),
        [typeof(NewtonsoftJsonSerialized<>)] = FormatterType(typeof(NewtonsoftJsonSerializedMemoryPackFormatter<>)),
        [typeof(TypeDecoratingSystemJsonSerialized<,>)]
            = FormatterType(typeof(TypeDecoratingSystemJsonSerializedMemoryPackFormatter<,>)),
        [typeof(TypeDecoratingNewtonsoftJsonSerialized<,>)]
            = FormatterType(typeof(TypeDecoratingNewtonsoftJsonSerializedMemoryPackFormatter<,>)),
        [typeof(RpcStream<>)] = FormatterType(typeof(RpcStreamMemoryPackFormatter<>)),
        [typeof(PageRef<>)] = FormatterType(typeof(PageRefMemoryPackFormatter<>)),
    };
    private static bool _isRegistered;
    // Guarded by Lock; non-null while a walk runs, which makes reentrant Register(Type) calls join it
    private static HashSet<Type>? _walkedTypes;
    private static List<(Type Type, object Formatter)>? _closedFormatters;

    public static void Register()
    {
        // A guard flag: Register runs once, but it's called on every type registration
        if (Volatile.Read(ref _isRegistered))
            return;

        lock (Lock) {
            if (_isRegistered)
                return;

            foreach (var (type, formatterType) in GenericFormatterTypes)
                MemoryPackFormatterProvider.RegisterGenericType(type, formatterType);

            // ActualLab.Core
            MemoryPackFormatterProvider.Register(new SymbolMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new ByteStringMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new TypeRefMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new FilePathMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new HostIdMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new JsonStringMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new TextOrBytesMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new ExceptionInfoMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new VersionSetMemoryPackFormatter());

            // ActualLab.Rpc
            MemoryPackFormatterProvider.Register(new RpcMethodRefMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new RpcHeaderKeyMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new RpcHeaderMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new RpcHandshakeMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new RpcCacheKeyMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new RpcCacheValueMemoryPackFormatter());

            // ActualLab.CommandR
            MemoryPackFormatterProvider.Register(new ServiceCallMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new OperationCompletionMemoryPackFormatter());

            // ActualLab.Fusion, ActualLab.Fusion.Ext.Contracts, ActualLab.Fusion.Ext.Backend.Contracts
            MemoryPackFormatterProvider.Register(new SessionMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new UserIdentityMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new UserMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new SessionAuthInfoMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new SessionInfoMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new AuthEditUserMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new AuthSignOutMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new AuthBackendSetSessionOptionsMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new AuthBackendSetupSessionMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new AuthBackendSignInMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new SandboxedKeyValueStoreSetMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new SandboxedKeyValueStoreRemoveMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new KeyValueStoreSetMemoryPackFormatter());
            MemoryPackFormatterProvider.Register(new KeyValueStoreRemoveMemoryPackFormatter());

            SerializerKind.MemoryPack.Register(
                () => MemoryPackByteSerializer.Default,
                () => MemoryPackByteSerializer.DefaultTypeDecorating);
            Volatile.Write(ref _isRegistered, true);
        }
    }

    // Registers the closed formatters of the generic types above found in T's generic arguments, array
    // element types and [MemoryPackable] members: MemoryPack would otherwise resolve an unmanaged closed
    // type like Option<int> to raw bytes there, while the generated formatters wrote a versioned object.
    public static void Register<T>()
        => Register(typeof(T));

    public static void Register(Type type)
    {
        if (RegisteredTypes.ContainsKey(type))
            return;

        Register();
        HashSet<Type> walkedTypes;
        List<(Type Type, object Formatter)> closedFormatters;
        lock (Lock) {
            if (_walkedTypes is not null) {
                // A formatter constructor called it during the walk: the walk's owner installs what it collects
                CollectClosedFormatters(type);
                return;
            }

            _walkedTypes = walkedTypes = [];
            _closedFormatters = closedFormatters = [];
            try {
                CollectClosedFormatters(type);
            }
            finally {
                _walkedTypes = null;
                _closedFormatters = null;
            }
        }
        // Installed outside the lock: MemoryPack constructs these formatters in its Cache<T> type initializers,
        // and their constructors call Register(Type), so writing Cache<T> under the lock could deadlock
        foreach (var (closedType, formatter) in closedFormatters)
            RegisterClosedFormatterMethod.MakeGenericMethod(closedType).Invoke(null, [formatter]);
        foreach (var walkedType in walkedTypes)
            RegisteredTypes.TryAdd(walkedType, default);
    }

    // Private methods

    private static void CollectClosedFormatters(Type type)
    {
        if (RegisteredTypes.ContainsKey(type) || !_walkedTypes!.Add(type))
            return;

        if (type.IsArray) {
            CollectClosedFormatters(type.GetElementType()!);
        }
        else if (type.IsGenericType && !type.ContainsGenericParameters) {
            var arguments = type.GetGenericArguments();
            foreach (var argument in arguments)
                CollectClosedFormatters(argument);
            if (GenericFormatterTypes.TryGetValue(type.GetGenericTypeDefinition(), out var formatterType)) {
                var formatter = Activator.CreateInstance(formatterType.MakeGenericType(arguments))!;
                _closedFormatters!.Add((type, formatter));
            }
        }
        if (type.IsDefined(typeof(MemoryPackableAttribute), inherit: false)) {
            // Only the members MemoryPack serializes: walking ignored ones may never terminate,
            // e.g. for Node<T> with an ignored Node<List<T>> member
            const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in type.GetFields(bindingFlags).Where(IsSerializedMember))
                CollectClosedFormatters(field.FieldType);
            foreach (var property in type.GetProperties(bindingFlags).Where(IsSerializedMember))
                CollectClosedFormatters(property.PropertyType);
        }
    }

    private static bool IsSerializedMember(MemberInfo member)
    {
        if (member.IsDefined(typeof(MemoryPackIgnoreAttribute), inherit: false))
            return false;
        if (member.IsDefined(typeof(MemoryPackIncludeAttribute), inherit: false))
            return true;

        return member switch {
            FieldInfo field => field.IsPublic,
            PropertyInfo property => property.GetMethod is { IsPublic: true },
            _ => false,
        };
    }

    private static void RegisterClosedFormatter<T>(object formatter)
    {
        // Keeps a formatter the app or an earlier walk registered, but replaces the one MemoryPack resolved
        // on its own, e.g. the raw-bytes formatter it picks for an unmanaged Option<int>
        if (MemoryPackFormatterProvider.IsRegistered<T>()
            && GetRegisteredFormatter<T>().GetType().Assembly != MemoryPackCoreAssembly)
            return;

        MemoryPackFormatterProvider.Register((MemoryPackFormatter<T>)formatter);
    }

    // MemoryPackFormatterProvider.GetFormatter<T> is internal, the reader exposes it
    private static IMemoryPackFormatter<T> GetRegisteredFormatter<T>()
    {
        var state = MemoryPackReaderOptionalStatePool.Rent(null);
        using var stateDisposer = (IDisposable)state;
        var reader = new MemoryPackReader(ReadOnlySpan<byte>.Empty, state);
        return reader.GetFormatter<T>();
    }

    // The annotation keeps the constructors that the walk and MemoryPack invoke via reflection
    private static Type FormatterType(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
        => type;
}
