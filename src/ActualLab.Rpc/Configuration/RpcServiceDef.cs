using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Internal;
using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc;

public sealed class RpcServiceDef
{
    private readonly ConcurrentDictionary<MethodInfo, RpcMethodDef?> _getOrFindMethodCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private Dictionary<MethodInfo, RpcMethodDef> _methods = null!;
    private Dictionary<Symbol, RpcMethodDef> _methodByName = null!;
    private string? _toStringCached;

    internal Dictionary<Symbol, RpcMethodDef> MethodByName => _methodByName;

    public RpcHub Hub { get; }
    public Type Type { get; }
    public ServiceResolver? ServerResolver { get; init; }
    public Symbol Name { get; init; }
    public bool IsSystem { get; init; }
    public bool IsBackend { get; init; }
    public bool HasServer => ServerResolver != null;
    [field: AllowNull, MaybeNull]
    public object Server => field ??= ServerResolver.Resolve(Hub.Services);
    public IReadOnlyCollection<RpcMethodDef> Methods => _methodByName.Values;
    public Symbol Scope { get; init; }
    public LegacyNames LegacyNames { get; init; }

    public RpcMethodDef this[MethodInfo method] => GetMethod(method) ?? throw Errors.NoMethod(Type, method);
    public RpcMethodDef this[Symbol methodName] => GetMethod(methodName) ?? throw Errors.NoMethod(Type, methodName);

    public RpcServiceDef(RpcHub hub, RpcServiceBuilder service)
    {
        var name = service.Name;
        if (name.IsEmpty)
            name = service.Type.GetName();

        Hub = hub;
        Name = name;
        Type = service.Type;
        ServerResolver = service.ServerResolver;
        IsSystem = typeof(IRpcSystemService).IsAssignableFrom(Type);
        IsBackend = hub.BackendServiceDetector.Invoke(service.Type);
        Scope = hub.ServiceScopeResolver.Invoke(this);
        LegacyNames = new LegacyNames(Type
            .GetCustomAttributes<LegacyNameAttribute>(false)
            .Select(x => LegacyName.New(x)));
    }

    internal void BuildMethods(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType)
    {
        if (serviceType != Type)
            throw new ArgumentOutOfRangeException(nameof(serviceType));

        _methods = new Dictionary<MethodInfo, RpcMethodDef>();
        _methodByName = new Dictionary<Symbol, RpcMethodDef>();
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
#pragma warning disable IL2067, IL2070
        var methods = (Type.IsInterface
                ? serviceType.GetAllInterfaceMethods(bindingFlags)
                : serviceType.GetMethods(bindingFlags)
            ).ToList();
#pragma warning restore IL2067, IL2070
        foreach (var method in methods) {
            if (method.DeclaringType == typeof(object))
                continue;
            if (method.IsGenericMethodDefinition)
                continue;

            var methodDef = Hub.MethodDefBuilder.Invoke(this, method);
            if (!methodDef.IsValid)
                continue;

            if (!_methodByName.TryAdd(methodDef.Name, methodDef))
                throw Errors.MethodNameConflict(methodDef);

            _methods.Add(method, methodDef);
        }
    }

    public override string ToString()
    {
        if (_toStringCached != null)
            return _toStringCached;

        var serverInfo = HasServer  ? $" -> {ServerResolver}" : "";
        var kindInfo = (IsSystem, IsBackend) switch {
            (true, true) => " [System,Backend]",
            (true, false) => " [System]",
            (false, true) => " [Backend]",
            _ => "",
        };
        return _toStringCached = $"'{Name}'{kindInfo}: {Type.GetName()}{serverInfo}, {Methods.Count} method(s)";
    }

    public RpcMethodDef? GetMethod(MethodInfo method)
        => _methods.GetValueOrDefault(method);
    public RpcMethodDef? GetMethod(Symbol methodName)
        => _methodByName.GetValueOrDefault(methodName);

    public RpcMethodDef? GetOrFindMethod(MethodInfo method)
        => _getOrFindMethodCache.GetOrAdd(method, static (methodInfo, self) => {
            var methodDef = self.GetMethod(methodInfo);
            if (methodDef != null)
                return methodDef;
            if (!methodInfo.IsPublic || typeof(InterfaceProxy).IsAssignableFrom(methodInfo.ReflectedType))
                return null;

            // It's a class proxy, let's try to map the method to interface
            var methodName = methodInfo.Name;
            var parameters = methodInfo.GetParameters();
            foreach (var m in self.Methods) {
                if (!m.Method.Name.Equals(methodName, StringComparison.Ordinal))
                    continue;

                if (m.Parameters.Length != parameters.Length)
                    continue;

                var isMatch = true;
                for (var i = 0; i < parameters.Length; i++) {
                    isMatch &= m.Parameters[i].ParameterType == parameters[i].ParameterType;
                    if (!isMatch)
                        break;
                }

                if (isMatch)
                    return m;
            }

            return null;
        }, this);
}
