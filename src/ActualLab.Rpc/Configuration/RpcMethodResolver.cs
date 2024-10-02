using ActualLab.Comparison;
using ActualLab.Internal;

namespace ActualLab.Rpc;

public sealed class RpcMethodResolver
{
    public readonly RpcServiceRegistry ServiceRegistry;
    public readonly VersionSet? Versions;
    public readonly IReadOnlyDictionary<RpcMethodRef, MethodEntry>? MethodByRef;
    public readonly IReadOnlyDictionary<Symbol, MethodEntry>? MethodByFullName;
    public readonly RpcMethodResolver? NextResolver;

    public RpcMethodDef? this[RpcMethodRef methodRef] {
        get {
            if (MethodByRef != null && MethodByRef.TryGetValue(methodRef, out var methodEntry))
                return methodEntry.Method;

            return NextResolver?[methodRef];
        }
    }

    public RpcMethodDef? this[Symbol fullName] {
        get {
            if (MethodByFullName != null && MethodByFullName.TryGetValue(fullName, out var methodEntry))
                return methodEntry.Method;

            return NextResolver?[fullName];
        }
    }

    public RpcMethodResolver(RpcServiceRegistry serviceRegistry, bool serverOnly)
    {
        ServiceRegistry = serviceRegistry;
        Versions = null;
        var methodByRef = new Dictionary<RpcMethodRef, MethodEntry>();
        var methodByFullName = new Dictionary<Symbol, MethodEntry>();
        foreach (var service in ServiceRegistry) {
            if (serverOnly && !service.HasServer)
                continue;

            foreach (var method in service.Methods) {
                var fullName = method.FullName.Value;
                var key = new RpcMethodRef(fullName, method);
                var entry = new MethodEntry(method, VersionExt.MaxValue);
                methodByRef.Add(key, entry);
                methodByFullName.Add(method.FullName, entry);
            }
        }
        MethodByRef = methodByRef;
        MethodByFullName = methodByFullName;
    }

    public RpcMethodResolver(RpcServiceRegistry serviceRegistry, VersionSet versions, RpcMethodResolver? nextResolver)
    {
        ServiceRegistry = serviceRegistry;
        Versions = versions;
        NextResolver = nextResolver;
        var methodByRef = new Dictionary<RpcMethodRef, MethodEntry>();
        var methodByFullName = new Dictionary<Symbol, MethodEntry>();
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

                var fullName = RpcMethodDef.ComposeFullName(serviceName.Value, methodName.Value);
                var key = new RpcMethodRef(fullName, method);
                var entry = new MethodEntry(method, methodVersion);
                if (!methodByRef.TryGetValue(key, out var existingEntry)) {
                    methodByRef.Add(key, entry);
                    methodByFullName.Add(fullName, entry);
                    continue;
                }

                var c = methodVersion.CompareTo(existingEntry.Version);
                if (c == 0)
                    throw Errors.Constraint(
                        $"[LegacyName] conflict: '{method.FullName}' and '{existingEntry.Method.FullName}' " +
                        $"are both mapped to '{serviceName}.{methodName}' in v{methodVersion.Format()}.");

                if (c < 0) {
                    // methodVersion < existingEntry.Version
                    methodByRef[key] = entry;
                    methodByFullName[fullName] = entry;
                }
            }
        }
        MethodByRef = methodByRef.Count != 0 ? methodByRef : null;
        MethodByFullName = methodByFullName.Count != 0 ? methodByFullName : null;
    }

    public override string ToString()
    {
        var sMethods = "[]";
        if (MethodByFullName != null) {
            var methods = MethodByFullName
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x =>
                    $"{Environment.NewLine}  '{x.Key}' -> '{x.Value.Method.FullName}' (v{x.Value.Version.Format()})");
            sMethods = "[" + string.Join("", methods) + Environment.NewLine + "]";
        }
        return $"{nameof(RpcMethodResolver)}(Versions = \"{Versions?.Format()}\", {nameof(MethodByRef)} = {sMethods})";
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
