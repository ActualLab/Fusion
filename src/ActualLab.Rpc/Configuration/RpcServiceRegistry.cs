using System.Diagnostics.CodeAnalysis;
using System.Text;
using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc;

public sealed class RpcServiceRegistry : RpcServiceBase, IReadOnlyCollection<RpcServiceDef>
{
    private readonly Dictionary<Type, RpcServiceDef> _services = new();
    private readonly Dictionary<Symbol, RpcServiceDef> _serviceByName = new();
    private readonly ConcurrentDictionary<VersionSet, RpcServerMethodResolver> _serverMethodResolvers = new();

    public static LogLevel ConstructionDumpLogLevel { get; set; } = OSInfo.IsAnyClient ? LogLevel.None : LogLevel.Information;

    public int Count => _serviceByName.Count;
    public RpcServiceDef this[Type serviceType] => Get(serviceType) ?? throw Errors.NoService(serviceType);
    public RpcServiceDef this[Symbol serviceName] => Get(serviceName) ?? throw Errors.NoService(serviceName);
    public RpcServerMethodResolver DefaultServerMethodResolver { get; }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<RpcServiceDef> GetEnumerator() => _serviceByName.Values.GetEnumerator();

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public RpcServiceRegistry(IServiceProvider services)
        : base(services)
    {
        var hub = Hub; // The implicit RpcHub resolution here freezes RpcConfiguration
        foreach (var (_, service) in hub.Configuration.Services) {
            var serviceDef = hub.ServiceDefBuilder.Invoke(hub, service);
            if (_serviceByName.TryGetValue(serviceDef.Name, out var existingServiceDef))
                throw Errors.ServiceNameConflict(serviceDef.Type, existingServiceDef.Type, serviceDef.Name);

            serviceDef.BuildMethods(serviceDef.Type);
            if (!_services.TryAdd(serviceDef.Type, serviceDef))
                throw Errors.ServiceTypeConflict(service.Type);

            _serviceByName.Add(serviceDef.Name, serviceDef);
        }
        DefaultServerMethodResolver = new RpcServerMethodResolver(this);
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
        if (log == null)
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
        foreach (var serviceDef in _serviceByName.Values.OrderBy(s => s.Name)) {
            sb.Append(indent).Append(serviceDef).AppendLine();
            if (!dumpMethods)
                continue;

            foreach (var methodDef in serviceDef.Methods.OrderBy(m => m.Name))
                sb.AppendLine($"{indent}- {methodDef.ToString(true)}");
        }
#pragma warning restore MA0011, MA0028, CA1305
    }

    public RpcServiceDef? Get<TService>()
        => Get(typeof(TService));

    public RpcServiceDef? Get(Type serviceType)
        => _services.GetValueOrDefault(serviceType);

    public RpcServiceDef? Get(Symbol serviceName)
        => _serviceByName.GetValueOrDefault(serviceName);

    public RpcServerMethodResolver GetServerMethodResolver(VersionSet? apiVersion)
    {
        if (apiVersion == null)
            return DefaultServerMethodResolver;

        return _serverMethodResolvers.GetOrAdd(apiVersion,
            static (key, self) => new RpcServerMethodResolver(self, key),
            this);
    }
}
