using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ActualLab.OS;
using Cysharp.Text;

namespace ActualLab.Reflection;

public static partial class TypeExt
{
#if NET7_0_OR_GREATER
    [GeneratedRegex("[^\\w\\d]+")]
    private static partial Regex MethodNameReFactory();
    [GeneratedRegex("_+$")]
    private static partial Regex MethodNameTailReFactory();
    [GeneratedRegex("`.+$")]
    private static partial Regex GenericTypeNameTailReFactory();

    private static readonly Regex MethodNameRe = MethodNameReFactory();
    private static readonly Regex MethodNameTailRe = MethodNameTailReFactory();
    private static readonly Regex GenericTypeNameTailRe = GenericTypeNameTailReFactory();
#else
    private static readonly Regex MethodNameRe = new("[^\\w\\d]+", RegexOptions.Compiled);
    private static readonly Regex MethodNameTailRe = new("_+$", RegexOptions.Compiled);
    private static readonly Regex GenericTypeNameTailRe = new("`.+$", RegexOptions.Compiled);
#endif

    private static readonly ConcurrentDictionary<Type, Type> NonProxyTypeCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(Type, bool, bool), LazySlim<(Type, bool, bool), Symbol>> GetNameCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<(Type, bool, bool), LazySlim<(Type, bool, bool), Symbol>> ToIdentifierNameCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<Type, LazySlim<Type, Symbol>> ToSymbolCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<Type, Type?> GetTaskOrValueTaskTypeCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<Type, object?> DefaultValueCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static readonly string SymbolPrefix = "@";

    public static Func<Type, Type> NonProxyTypeResolver {
        get;
        set {
            field = value;
            NonProxyTypeCache.Clear();
        }
    } = DefaultNonProxyTypeResolver;

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume all used constructors are preserved")]
    public static object? GetDefaultValue(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.NonPublicConstructors)] this Type type)
    {
        if (!type.IsValueType)
            return null;
        return DefaultValueCache.GetOrAdd(type, static type => {
#if !NETSTANDARD2_0
            return RuntimeHelpers.GetUninitializedObject(type);
#else
            return FormatterServices.GetUninitializedObject(type);
#endif
        });
    }

    public static Type NonProxyType(this Type type)
        => NonProxyTypeCache.GetOrAdd(type, NonProxyTypeResolver);

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume all base types and interfaces are preserved")]
    public static IEnumerable<Type> GetAllBaseTypes(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] this Type type,
        bool addSelf = false, bool addInterfaces = false)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        if (addSelf) {
            yield return type;
            if (type == typeof(object))
                yield break;
        }

        var baseType = type.BaseType;
        while (baseType != typeof(object) && baseType != null) {
            yield return baseType;
            baseType = baseType.BaseType;
        }
        if (addInterfaces) {
            var interfaces = type.GetInterfaces();
            if (interfaces.Length == 0)
                yield break;

            var orderedInterfaces = interfaces
                .OrderBy(i => -i.GetInterfaces().Length)
                .OrderByDependency(i => interfaces.Where(j => i != j && j.IsAssignableFrom(i)))
                .Reverse();
            foreach (var @interface in orderedInterfaces)
                yield return @interface;
        }

        yield return typeof(object);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "We assume all required methods are preserved")]
    public static IEnumerable<MethodInfo> GetAllInterfaceMethods(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] this Type type,
        BindingFlags bindingFlags,
        Func<Type, bool>? interfaceFilter = null)
    {
        if (!type.IsInterface)
            yield break;

        var baseTypes = type.GetAllBaseTypes(true, true);
        if (interfaceFilter != null)
            baseTypes = baseTypes.Where(interfaceFilter);
        foreach (var baseType in baseTypes)
        foreach (var method in baseType.GetMethods(bindingFlags))
            yield return method;
    }

    public static bool MayCastSucceed(this Type castFrom, Type castTo)
    {
        if (castTo.IsSealed || castTo.IsValueType)
            // AnyBase(SealedType) -> SealedType
            return castFrom.IsAssignableFrom(castTo);
        if (castFrom.IsSealed || castFrom.IsValueType)
            // SealedType -> AnyBase(SealedType)
            return castTo.IsAssignableFrom(castFrom);
        if (castTo.IsInterface || castFrom.IsInterface)
            // Not super obvious, but true
            return true;

        // Both types are classes, so the cast may succeed
        // only if one of them is a base of another
        return castTo.IsAssignableFrom(castFrom) || castFrom.IsAssignableFrom(castTo);
    }

    public static string GetName(this Type type, bool useFullName = false, bool useFullArgumentNames = false)
    {
        var key = (type, useFullName, useFullArgumentNames);
        return GetNameCache.GetOrAdd(key,
            static key1 => {
                var (type1, useFullName1, useFullArgumentNames1) = key1;
                var name = type1.Name;
                if (type1.IsGenericTypeDefinition) {
                    name = GenericTypeNameTailRe.Replace(name, "");
                    var argumentNames = type1.GetGenericArguments().Select(t => t.Name);
                    name = $"{name}<{argumentNames.ToDelimitedString(",")}>";
                }
                else if (type1.IsGenericType) {
                    name = GenericTypeNameTailRe.Replace(name, "");
                    var argumentNames = type1.GetGenericArguments()
                        .Select(t => t.GetName(useFullArgumentNames1, useFullArgumentNames1));
                    name = $"{name}<{argumentNames.ToDelimitedString(",")}>";
                }
                if (type1.DeclaringType != null)
                    name = $"{type1.DeclaringType.GetName(useFullName1)}+{name}";
                else if (useFullName1)
                    name = $"{type1.Namespace}.{name}";
                return name;
            });
    }

    public static string ToIdentifierName(this Type type, bool useFullName = false, bool useFullArgumentNames = false)
    {
        var key = (type, useFullName, useFullArgumentNames);
        return ToIdentifierNameCache.GetOrAdd(key,
            static key1 => {
                var (type1, useFullName1, useFullArgumentNames1) = key1;
                var name = type1.Name;
                if (type1.IsGenericTypeDefinition)
                    name = $"{GenericTypeNameTailRe.Replace(name, "")}_{type1.GetGenericArguments().Length}";
                else if (type1.IsGenericType) {
                    name = GenericTypeNameTailRe.Replace(name, "");
                    var argumentNames = type1.GetGenericArguments()
                        .Select(t => t.ToIdentifierName(useFullArgumentNames1, useFullArgumentNames1));
                    name = string.Join("_", EnumerableExt.One(name).Concat(argumentNames));
                }
                if (type1.DeclaringType != null)
                    name = $"{type1.DeclaringType.ToIdentifierName(useFullName1)}_{name}";
                else if (useFullName1)
                    name = $"{type1.Namespace}_{name}";
                name = MethodNameRe.Replace(name, "_");
                name = MethodNameTailRe.Replace(name, "");
                return name;
            });
    }

    public static Symbol ToSymbol(this Type type)
        => ToSymbolCache.GetOrAdd(type,
                static type1 => new Symbol(SymbolPrefix + type1.ToIdentifierName(true, true)));

    public static Symbol ToSymbol(this Type type, bool withPrefix)
        => withPrefix
            ? type.ToSymbol()
            : (Symbol)type.ToIdentifierName(true, true);

    public static bool IsTaskOrValueTask(this Type type)
        => type.GetTaskOrValueTaskType() != null;

    public static Type? GetTaskOrValueTaskType(this Type type)
    {
        return GetTaskOrValueTaskTypeCache.GetOrAdd(type, static type1 => {
            if (type1 == typeof(object))
                return null;
            if (type1 == typeof(ValueTask) || type1 == typeof(Task))
                return type1;
            if (type1.IsGenericType) {
                var gtd = type1.GetGenericTypeDefinition();
                if (gtd == typeof(ValueTask<>) || gtd == typeof(Task<>))
                    return type1;
            }

            var baseType = type1.BaseType;
            return baseType == null ? null : GetTaskOrValueTaskType(baseType);
        });
    }

    public static Type? GetTaskOrValueTaskArgument(this Type type)
    {
        var taskType = type.GetTaskOrValueTaskType();
        if (taskType == null)
            throw new ArgumentOutOfRangeException(nameof(type));
        return taskType.IsGenericType
            ? taskType.GenericTypeArguments.SingleOrDefault()
            : null;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume proxy-related code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume proxy-related code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume proxy-related code is preserved")]
    public static Type DefaultNonProxyTypeResolver(Type type)
    {
        const string proxyNamespace = "ActualLabProxies";
        const string proxyNamespaceSuffix = "." + proxyNamespace;
        const string proxy = "Proxy";

        var @namespace = type.Namespace ?? "";
        var hasProxyNamespaceSuffix = @namespace.EndsWith(proxyNamespaceSuffix, StringComparison.Ordinal);
        if (!hasProxyNamespaceSuffix && !@namespace.Equals(proxyNamespace, StringComparison.Ordinal))
            return type;

        if (type.IsConstructedGenericType) {
            var genericType = type.GetGenericTypeDefinition();
            var genericProxyType = DefaultNonProxyTypeResolver(genericType);
            return genericType == genericProxyType
                ? type
                : genericProxyType.MakeGenericType(type.GenericTypeArguments);
        }

        var name = type.Name;
        var namePrefix = name;
        var nameSuffix = "";
        if (type.IsGenericTypeDefinition) {
            var backTrickIndex = name.IndexOf('`', StringComparison.Ordinal);
            if (backTrickIndex < 0)
                return type; // Weird case, shouldn't happen

            namePrefix = name[..backTrickIndex];
            nameSuffix = name[backTrickIndex..];
        }

        if (!namePrefix.EndsWith(proxy, StringComparison.Ordinal))
            return type;

        var nonProxyNamespacePrefix = @namespace[..^proxyNamespace.Length];
        var nonProxyNamePrefix = namePrefix[..^proxy.Length];
        var nonProxyName = ZString.Concat(nonProxyNamespacePrefix, nonProxyNamePrefix, nameSuffix);
        try {
            return type.Assembly.GetType(nonProxyName) ?? type;
        }
        catch {
            return type;
        }
    }
}
