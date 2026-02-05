using System.Text;
using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

/// <summary>
/// Registry of all RPC service definitions, supporting lookup by type, name, and method resolution.
/// </summary>
public class RpcServiceRegistry : RpcServiceBase, IReadOnlyCollection<RpcServiceDef>
{
    private readonly Dictionary<Type, RpcServiceDef> _services = new();
    private readonly Dictionary<string, RpcServiceDef> _serviceByName = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<VersionSet, RpcMethodResolver> _legacyServerMethodResolvers = new();

    public static LogLevel ConstructionDumpLogLevel { get; set; } = OSInfo.IsAnyClient ? LogLevel.None : LogLevel.Information;

    public int Count => _serviceByName.Count;
    public RpcServiceDef this[Type serviceType] => Get(serviceType) ?? throw Errors.NoService(serviceType);
    public RpcServiceDef this[string serviceName] => Get(serviceName) ?? throw Errors.NoService(serviceName);
#pragma warning disable CA1721
    public RpcMethodResolver ServerMethodResolver { get; }
#pragma warning restore CA1721
    public RpcMethodResolver AnyMethodResolver { get; }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<RpcServiceDef> GetEnumerator() => _serviceByName.Values.GetEnumerator();

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume RPC-related code is fully preserved")]
    public RpcServiceRegistry(IServiceProvider services)
        : base(services)
    {
        var hub = Hub!; // The implicit RpcHub resolution here freezes RpcConfiguration
        foreach (var (_, service) in hub.Configuration.Services) {
            if (service.Mode == RpcServiceMode.Local)
                continue; // Skip all local services
            if (service.Mode == RpcServiceMode.Default)
                throw Errors.UnspecifiedServiceMode(service.Type, service.Mode);

            var serviceDef = hub.RegistryOptions.ServiceDefFactory.Invoke(hub, service);
            serviceDef.InitializeOverridableProperties(methodsReady: false);
            if (_serviceByName.TryGetValue(serviceDef.Name, out var existingServiceDef))
                throw Errors.ServiceNameConflict(serviceDef.Type, existingServiceDef.Type, serviceDef.Name);

            serviceDef.BuildMethods(serviceDef.Type);
            serviceDef.InitializeOverridableProperties(methodsReady: true);
            if (!_services.TryAdd(serviceDef.Type, serviceDef))
                throw Errors.ServiceTypeConflict(service.Type);
            if (serviceDef.ClientType is { } clientType && clientType != serviceDef.Type)
                if (!_services.TryAdd(clientType, serviceDef))
                    throw Errors.ServiceTypeConflict(service.Type);

            _serviceByName.Add(serviceDef.Name, serviceDef);
        }
        AnyMethodResolver = new RpcMethodResolver(this, serverOnly: false, Log);
        ServerMethodResolver = new RpcMethodResolver(this, serverOnly: true, null);
        DumpTo(Log, ConstructionDumpLogLevel, "Registered services:");
    }

    public override string ToString()
        => $"{GetType().GetName()}({_services.Count} service(s))";

    public string Dump(bool dumpMethods = true, string indent = "")
    {
        var sb = StringBuilderExt.Acquire();
        DumpTo(sb, dumpMethods, indent);
        return sb.ToStringAndRelease().TrimEnd();
    }

    public void DumpTo(ILogger? log, LogLevel logLevel, string title, bool dumpMethods = true)
    {
        log = log.IfEnabled(logLevel);
        if (log is null)
            return;

        var sb = StringBuilderExt.Acquire();
        sb.AppendLine(title);
        DumpTo(sb, dumpMethods);
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        log.Log(logLevel, sb.ToStringAndRelease().TrimEnd());
    }

    public void DumpTo(StringBuilder sb, bool dumpMethods = true, string indent = "")
    {
#pragma warning disable MA0011, MA0028, CA1305
        foreach (var serviceDef in _serviceByName.Values.OrderBy(s => s.Name, StringComparer.Ordinal)) {
            sb.Append(indent).Append(serviceDef).AppendLine();
            if (!dumpMethods)
                continue;

            foreach (var methodDef in serviceDef.Methods.OrderBy(m => m.Name, StringComparer.Ordinal))
                sb.AppendLine($"{indent}- {methodDef.ToString(true)}");
        }
#pragma warning restore MA0011, MA0028, CA1305
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcServiceDef? Get<TService>()
        => Get(typeof(TService));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcServiceDef? Get(Type serviceType)
        => _services.GetValueOrDefault(serviceType);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcServiceDef? Get(string serviceName)
        => _serviceByName.GetValueOrDefault(serviceName);

    public RpcMethodResolver GetServerMethodResolver(VersionSet? versions)
    {
        if (versions is null)
            return ServerMethodResolver;

        return _legacyServerMethodResolvers.GetOrAdd(
            versions,
            static (versions, self) => new RpcMethodResolver(self, versions, self.ServerMethodResolver, self.Log),
            this);
    }
}
