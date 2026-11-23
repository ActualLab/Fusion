using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// A MessagePack resolver that honors the [MessagePackObject] / [DataContract] contracts of the types
/// <see cref="TypeFilter"/> accepts, but serializes their members by name as a string-keyed map,
/// ignoring [Key(N)]. It builds formatters with MessagePack's own dynamic object formatter builder
/// (via reflection: there is no public switch for this), so it doesn't work under NativeAOT.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "MessagePack's own types are preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "MessagePack's own types are preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "MessagePack's own types are preserved")]
[UnconditionalSuppressMessage("Trimming", "IL2090", Justification = "We assume serializable types are fully preserved")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Keyed MessagePack formats require dynamic code")]
public sealed class StringKeyMessagePackResolver : IFormatterResolver
{
    private static readonly Assembly MessagePackAssembly = typeof(MessagePackSerializer).Assembly;
#if NET8_0_OR_GREATER // MessagePack v3
    private static readonly object DynamicAssemblyFactory = CreateInternal(
        "MessagePack.Internal.DynamicAssemblyFactory", nameof(StringKeyMessagePackResolver));
    private static readonly MethodInfo BuildFormatterHelperMethod =
        typeof(DynamicObjectResolver).GetMethod("BuildFormatterHelper", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw Errors.UnsupportedMessagePackVersion("DynamicObjectResolver.BuildFormatterHelper");
#else // MessagePack v2
    private static readonly object DynamicAssembly = CreateInternal(
        "MessagePack.Internal.DynamicAssembly", nameof(StringKeyMessagePackResolver));
    private static readonly MethodInfo BuildTypeMethod =
        MessagePackAssembly.GetType("MessagePack.Internal.DynamicObjectTypeBuilder", throwOnError: false)
            ?.GetMethod("BuildType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw Errors.UnsupportedMessagePackVersion("DynamicObjectTypeBuilder.BuildType");
#endif

    private static readonly ConcurrentDictionary<Assembly, bool> IsFrameworkAssemblyCache = new();

    public static readonly StringKeyMessagePackResolver Instance = new();

    // ActualLab's own types (the ones in assemblies marked with "ActualLab.Framework" metadata) keep
    // their [Key(N)] layout: e.g. RPC system calls carry them, and every peer reads them the same way
    public static Func<Type, bool> TypeFilter { get; set; } = static type => !IsFrameworkAssembly(type.Assembly);

    public static bool IsFrameworkAssembly(Assembly assembly)
        => IsFrameworkAssemblyCache.GetOrAdd(assembly, static a => a
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(x => string.Equals(x.Key, "ActualLab.Framework", StringComparison.Ordinal)));

    private StringKeyMessagePackResolver()
    { }

    public IMessagePackFormatter<T>? GetFormatter<T>()
        => FormatterCache<T>.Formatter;

    // Private methods

    private static object CreateInternal(string typeName, string name)
    {
        var type = MessagePackAssembly.GetType(typeName, throwOnError: false)
            ?? throw Errors.UnsupportedMessagePackVersion(typeName);
        return Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, [name], culture: null)!;
    }

    private static IMessagePackFormatter<T>? CreateFormatter<T>()
    {
        var type = typeof(T);
        if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition || !TypeFilter.Invoke(type))
            return null;
        if (!type.IsDefined(typeof(MessagePackObjectAttribute), false)
            && !type.IsDefined(typeof(DataContractAttribute), false))
            return null;

#if NET8_0_OR_GREATER
        return (IMessagePackFormatter<T>?)BuildFormatterHelperMethod.MakeGenericMethod(type).Invoke(null, [
            Instance, DynamicAssemblyFactory, /* forceStringKey */ true, /* contractless */ false, /* allowPrivate */ false,
        ]);
#else
        var formatterType = (TypeInfo?)BuildTypeMethod.Invoke(null, [
            DynamicAssembly, type, /* forceStringKey */ true, /* contractless */ false,
        ]);
        return formatterType is null
            ? null
            : (IMessagePackFormatter<T>)Activator.CreateInstance(formatterType.AsType())!;
#endif
    }

    // Nested types

    private static class FormatterCache<T>
    {
        public static readonly IMessagePackFormatter<T>? Formatter = CreateFormatter<T>();
    }
}
