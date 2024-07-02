using ActualLab.Comparison;
using ActualLab.Internal;

namespace ActualLab.Rpc;

public sealed class RpcServerMethodResolver
{
    public RpcServiceRegistry ServiceRegistry { get; }
    public VersionSet? Versions { get; }
    public IReadOnlyDictionary<(Symbol, Symbol), MethodEntry>? LegacyMethods { get; }

    public RpcMethodDef? this[Symbol serviceName, Symbol methodName] {
        get {
            if (LegacyMethods != null && LegacyMethods.TryGetValue((serviceName, methodName), out var methodEntry))
                return methodEntry.Method;

            // Default flow
            var service = ServiceRegistry.Get(serviceName);
            return service is { HasServer: true }
                ? service.GetMethod(methodName)
                : null;
        }
    }

    public RpcServerMethodResolver(RpcServiceRegistry serviceRegistry)
    {
        ServiceRegistry = serviceRegistry;
        Versions = null;
        LegacyMethods = null;
    }

    public RpcServerMethodResolver(RpcServiceRegistry serviceRegistry, VersionSet versions)
    {
        ServiceRegistry = serviceRegistry;
        Versions = versions;
        var legacyMethods = new Dictionary<(Symbol, Symbol), MethodEntry>();
        foreach (var service in ServiceRegistry) {
            if (!service.HasServer)
                continue; // No need to remap clients

            var scope = service.Scope;
            var version = versions[scope];
            var legacyServiceName = service.LegacyNames[version];
            var serviceName = legacyServiceName.Name.Or(service.Name);
            var serviceVersion = legacyServiceName.MaxVersion;
            foreach (var method in service.Methods) {
                var legacyMethodName = method.LegacyNames[version];
                if (legacyMethodName.IsNone && legacyServiceName.IsNone)
                    continue; // No overrides

                var methodName = legacyMethodName.Name.Or(method.Name);
                var methodVersion = legacyMethodName.IsNone ? serviceVersion : legacyMethodName.MaxVersion;

                var key = (serviceName, methodName);
                if (!legacyMethods.TryGetValue(key, out var existingEntry)) {
                    legacyMethods.Add(key, new MethodEntry(method, methodVersion));
                    continue;
                }

                var c = methodVersion.CompareTo(existingEntry.Version);
                if (c == 0)
                    throw Errors.Constraint(
                        $"[LegacyName] conflict: '{method.FullName}' and '{existingEntry.Method.FullName}' " +
                        $"are both mapped to '{serviceName}.{methodName}' in v{methodVersion.Format()}.");

                if (c < 0) // methodVersion < existingEntry.Version
                    legacyMethods[key] = new MethodEntry(method, methodVersion);
            }
        }
        LegacyMethods = legacyMethods.Count != 0 ? legacyMethods : null;
    }

    public override string ToString()
    {
        var legacyMethods = LegacyMethods == null
            ? "[]"
            : "[" + string.Join("", LegacyMethods.OrderBy(x => x.Key).Select(x => $"{Environment.NewLine}  '{x.Key.Item1}.{x.Key.Item2}' -> '{x.Value.Method.FullName}' (v{x.Value.Version.Format()})"))
                  + Environment.NewLine + "]";
        return $"{nameof(RpcServerMethodResolver)}(Versions = \"{Versions?.Format()}\", {nameof(LegacyMethods)} = {legacyMethods})";
    }

    // Nested type

    public readonly record struct MethodEntry(
        RpcMethodDef Method,
        Version Version)
    {
        public override string ToString()
            => $"{Method} (v{Version.Format()})";
    }
}
