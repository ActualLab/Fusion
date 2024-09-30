using ActualLab.Comparison;
using ActualLab.Internal;

namespace ActualLab.Rpc;

public sealed class RpcServerMethodResolver
{
    public RpcServiceRegistry ServiceRegistry { get; }
    public VersionSet? Versions { get; }
    public IReadOnlyDictionary<RpcMethodRef, MethodEntry>? Methods { get; }
    public bool IsDefault { get; }

    public RpcMethodDef? this[RpcMethodRef methodRef] {
        get {
            if (Methods != null && Methods.TryGetValue(methodRef, out var methodEntry))
                return methodEntry.Method;

            return IsDefault ? null : ServiceRegistry.DefaultServerMethodResolver[methodRef];
        }
    }

    public RpcServerMethodResolver(RpcServiceRegistry serviceRegistry)
    {
        ServiceRegistry = serviceRegistry;
        Versions = null;
        var methods = new Dictionary<RpcMethodRef, MethodEntry>();
        foreach (var service in ServiceRegistry) {
            if (!service.HasServer)
                continue; // No need to remap clients

            foreach (var method in service.Methods) {
                var key = new RpcMethodRef(service.Name.Value, method.Name.Value, method);
                methods.Add(key, new MethodEntry(method, VersionExt.MaxValue));
            }
        }
        Methods = methods;
        IsDefault = true;
    }

    public RpcServerMethodResolver(RpcServiceRegistry serviceRegistry, VersionSet versions)
    {
        ServiceRegistry = serviceRegistry;
        Versions = versions;
        var methods = new Dictionary<RpcMethodRef, MethodEntry>();
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

                var key = new RpcMethodRef(serviceName.Value, methodName.Value, method);
                if (!methods.TryGetValue(key, out var existingEntry)) {
                    methods.Add(key, new MethodEntry(method, methodVersion));
                    continue;
                }

                var c = methodVersion.CompareTo(existingEntry.Version);
                if (c == 0)
                    throw Errors.Constraint(
                        $"[LegacyName] conflict: '{method.FullName}' and '{existingEntry.Method.FullName}' " +
                        $"are both mapped to '{serviceName}.{methodName}' in v{methodVersion.Format()}.");

                if (c < 0) // methodVersion < existingEntry.Version
                    methods[key] = new MethodEntry(method, methodVersion);
            }
        }
        Methods = methods.Count != 0 ? methods : null;
    }

    public override string ToString()
    {
        var sMethods = "[]";
        if (!IsDefault && Methods != null) {
            var methods = Methods
                .OrderBy(x => x.Key.Id.ToStringAsUtf8(), StringComparer.Ordinal)
                .Select(x =>
                    $"{Environment.NewLine}  '{x.Key.GetFullMethodName()}' -> '{x.Value.Method.FullName}' (v{x.Value.Version.Format()})");
            sMethods = "[" + string.Join("", methods) + Environment.NewLine + "]";
        }
        return $"{nameof(RpcServerMethodResolver)}(Versions = \"{Versions?.Format()}\", {nameof(Methods)} = {sMethods})";
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
