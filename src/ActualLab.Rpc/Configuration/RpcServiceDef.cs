using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc;

/// <summary>
/// Describes a registered RPC service, including its type, mode, methods, and server/client instances.
/// </summary>
public class RpcServiceDef
{
    private readonly ConcurrentDictionary<MethodInfo, RpcMethodDef?> _findMethodCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private Dictionary<MethodInfo, RpcMethodDef> _methods = null!;
    private Dictionary<string, RpcMethodDef> _methodByName = null!;
    private readonly Lazy<object?> _serverLazy;
    private readonly Lazy<object?> _clientLazy;
    private readonly LazySlim<RpcPolymorphicArgumentHandlerIsValidCallFunc?> _polymorphicArgumentHandlerIsValidCallFuncLazy;
    private string? _toStringCached;

    public RpcHub Hub { get; }
    public Type Type { get; }
    public ServiceResolver? ServerResolver { get; init; }
    public Type? ServerType { get; }
    public Type? ClientType { get; }
    public string Name { get; init; }
    public RpcServiceMode Mode { get; init; }
    public bool IsSystem { get; init; }
    public bool IsBackend { get; init; }
    public object? Server => _serverLazy.Value;
    public object? Client => _clientLazy.Value;
    public bool HasClient => ClientType is not null;
    public bool HasServer => ServerResolver is not null;
    public IReadOnlyCollection<RpcMethodDef> Methods => _methodByName.Values;
    public RpcLocalExecutionMode LocalExecutionMode { get; init; }
    public string Scope { get; init; }
    public LegacyNames LegacyNames { get; init; }
    public PropertyBag Properties { get; protected set; }

    // Purely to speed up a frequent cast operation in RpcInboundCall.DeserializeArguments for Ok method
    public RpcPolymorphicArgumentHandlerIsValidCallFunc? PolymorphicArgumentHandlerIsValidCallFunc
        => _polymorphicArgumentHandlerIsValidCallFuncLazy.Value;

    public RpcMethodDef this[MethodInfo method] => GetMethod(method) ?? throw Errors.NoMethod(Type, method);
    public RpcMethodDef this[string methodName] => GetMethod(methodName) ?? throw Errors.NoMethod(Type, methodName);

    public RpcServiceDef(RpcHub hub, RpcServiceBuilder service)
    {
        var name = service.Name.NullIfEmpty() ?? service.Type.GetName();
        Hub = hub;
        Name = name;
        Mode = service.Mode;
        Type = service.Type;
        ServerResolver = service.ImplementationResolver;
        ServerType = ServerResolver?.Type;
        ClientType = service.ClientType;
        IsSystem = typeof(IRpcSystemService).IsAssignableFrom(Type);
        IsBackend = typeof(IBackendService).IsAssignableFrom(Type);
        LocalExecutionMode = service.LocalExecutionMode;
        Scope = hub.RegistryOptions.ServiceScopeResolver.Invoke(this);
        LegacyNames = new LegacyNames(Type);

        _serverLazy = new Lazy<object?>(() => ServerResolver?.Resolve(Hub.Services));
        _clientLazy = new Lazy<object?>(() => ClientType is null ? null : Hub.Services.GetRequiredService(ClientType));
        _polymorphicArgumentHandlerIsValidCallFuncLazy = new LazySlim<RpcPolymorphicArgumentHandlerIsValidCallFunc?>(
            () => Server is IRpcPolymorphicArgumentHandler h
                ? h.IsValidCall
                : null);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume RPC-related code is fully preserved")]
    internal void BuildMethods([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type serviceType)
    {
        if (serviceType != Type)
            throw new ArgumentOutOfRangeException(nameof(serviceType));

        _methods = new Dictionary<MethodInfo, RpcMethodDef>();
        _methodByName = new Dictionary<string, RpcMethodDef>(StringComparer.Ordinal);
        var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
        var methods = (Type.IsInterface
                ? serviceType.GetAllInterfaceMethods(bindingFlags)
                : serviceType.GetMethods(bindingFlags)
            ).ToList();
        foreach (var method in methods) {
            if (method.DeclaringType == typeof(object))
                continue;
            if (method.IsGenericMethodDefinition)
                continue;

            var methodDef = Hub.RegistryOptions.MethodDefFactory.Invoke(this, method);
            if (!methodDef.IsValid)
                continue;

            methodDef.InitializeOverridableProperties();
            if (!_methodByName.TryAdd(methodDef.Name, methodDef))
                throw Errors.MethodNameConflict(methodDef);

            _methods.Add(method, methodDef);
        }
    }

    public override string ToString()
    {
        if (_toStringCached is not null)
            return _toStringCached;

        var serverInfo = HasServer  ? $" -> {ServerResolver}" : "";
        var flags = (IsSystem, IsBackend) switch {
            (true, true) => ",System,Backend",
            (true, false) => ",System",
            (false, true) => ",Backend",
            _ => "",
        };
        var attributes = $"[{Mode:G}{flags}]";
        return _toStringCached = $"'{Name}'{attributes}: {Type.GetName()}{serverInfo}, {Methods.Count} method(s)";
    }

    public RpcMethodDef? GetMethod(MethodInfo method)
        => _methods.GetValueOrDefault(method);
    public RpcMethodDef? GetMethod(string methodName)
        => _methodByName.GetValueOrDefault(methodName);

    public RpcMethodDef? FindMethod(MethodInfo method)
    {
        if (!method.IsPublic)
            return null;

        return _findMethodCache.GetOrAdd(method, static (methodInfo, self) => {
            if (self.GetMethod(methodInfo) is { } methodDef)
                return methodDef;

            // Lookup failed, let's try to match it to one of our methods
            var methodName = methodInfo.Name;
            var parameters = methodInfo.GetParameters();
            foreach (var m in self.Methods) {
                if (!m.MethodInfo.Name.Equals(methodName, StringComparison.Ordinal))
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

    public virtual void InitializeOverridableProperties(bool methodsReady)
    { }
}
