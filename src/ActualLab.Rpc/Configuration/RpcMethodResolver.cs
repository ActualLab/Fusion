#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using ActualLab.Comparison;
using ActualLab.Internal;
using ActualLab.OS;

namespace ActualLab.Rpc;

public sealed class RpcMethodResolver
{
    public readonly RpcServiceRegistry ServiceRegistry;
    public readonly VersionSet? Versions;
    public readonly IReadOnlyDictionary<Symbol, MethodEntry>? MethodByFullName;
    public readonly IReadOnlyDictionary<RpcMethodRef, MethodEntry>? MethodByRef;
    public readonly IReadOnlyDictionary<int, MethodEntry>? MethodByHashCode;
    public readonly RpcMethodResolver? NextResolver;

    public RpcMethodDef? this[in RpcMethodRef methodRef] {
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

    public RpcMethodDef? this[int id] {
        get {
            if (MethodByHashCode != null && MethodByHashCode.TryGetValue(id, out var methodEntry))
                return methodEntry.Method;

            return NextResolver?[id];
        }
    }

    public RpcMethodResolver(RpcServiceRegistry serviceRegistry, bool serverOnly, ILogger? log)
    {
        ServiceRegistry = serviceRegistry;
        Versions = null;
        var methodByFullName = new Dictionary<Symbol, MethodEntry>();
        var methodByRef = new Dictionary<RpcMethodRef, MethodEntry>();
        var methodByHashCode = new Dictionary<int, MethodEntry>();
        foreach (var service in ServiceRegistry) {
            if (serverOnly && !service.HasServer)
                continue;

            foreach (var method in service.Methods) {
                var fullName = method.FullName.Value;
                var methodRef = new RpcMethodRef(fullName, method);
                var hashCode = methodRef.HashCode;
                var entry = new MethodEntry(method, VersionExt.MaxValue);
                methodByFullName.Add(method.FullName, entry);
                methodByRef.Add(methodRef, entry);
                methodByHashCode[hashCode] = entry;
            }
        }
        if (methodByHashCode.Count != methodByRef.Count) {
            var conflicts = methodByRef.Keys
                .GroupBy(x => x.HashCode)
                .Where(g => g.Count() > 1)
                .SelectMany(x => x)
                .OrderBy(x => x.HashCode).ThenBy(x => x.GetFullMethodName(), StringComparer.Ordinal)
                .ToList();
            foreach (var methodRef in conflicts) {
                log?.LogError("RpcMethodRef.HashCode conflict for {MethodRef}", methodRef);
                methodByHashCode[methodRef.HashCode] = default;
            }
        }

        MethodByFullName = methodByFullName;
        MethodByRef = methodByRef;
        MethodByHashCode = methodByHashCode;
#if NET8_0_OR_GREATER
        var useFrozenDictionary = RpcDefaults.Mode == RpcMode.Server;
        if (useFrozenDictionary) {
            MethodByFullName = methodByFullName.ToFrozenDictionary();
            MethodByRef = methodByRef.ToFrozenDictionary();
            MethodByHashCode = methodByHashCode.ToFrozenDictionary();
        }
#endif
    }

    public RpcMethodResolver(RpcServiceRegistry serviceRegistry, VersionSet versions, RpcMethodResolver nextResolver, ILogger? log)
    {
        ServiceRegistry = serviceRegistry;
        Versions = versions;
        NextResolver = nextResolver;
        var methodByRef = new Dictionary<RpcMethodRef, MethodEntry>();
        var methodByFullName = new Dictionary<Symbol, MethodEntry>();
        var methodByHashCode = new Dictionary<int, MethodEntry>();
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
                var methodRef = new RpcMethodRef(fullName, method);
                var hashCode = methodRef.HashCode;
                var entry = new MethodEntry(method, methodVersion);
                if (!methodByRef.TryGetValue(methodRef, out var existingEntry)) {
                    methodByRef.Add(methodRef, entry);
                    methodByFullName.Add(fullName, entry);
                    methodByHashCode[hashCode] = entry;
                    continue;
                }

                var c = methodVersion.CompareTo(existingEntry.Version);
                if (c == 0)
                    throw Errors.Constraint(
                        $"[LegacyName] conflict: '{method.FullName}' and '{existingEntry.Method.FullName}' " +
                        $"are both mapped to '{serviceName}.{methodName}' in v{methodVersion.Format()}.");

                if (c < 0) {
                    // methodVersion < existingEntry.Version
                    methodByRef[methodRef] = entry;
                    methodByFullName[fullName] = entry;
                    methodByHashCode[hashCode] = entry;
                }
            }
        }
        var conflicts = methodByRef.Keys.Concat(nextResolver.MethodByRef?.Keys ?? [])
            .GroupBy(x => x.HashCode)
            .Where(g => g.Count() > 1)
            .SelectMany(x => x)
            .OrderBy(x => x.HashCode).ThenBy(x => x.GetFullMethodName(), StringComparer.Ordinal)
            .ToList();
        foreach (var methodRef in conflicts) {
            log?.LogError("RpcMethodRef.HashCode conflict for {MethodRef} @ {VersionSet}", methodRef, versions);
            methodByHashCode[methodRef.HashCode] = default;
        }

        MethodByRef = methodByRef.Count != 0 ? methodByRef : null;
        MethodByFullName = methodByFullName.Count != 0 ? methodByFullName : null;
        MethodByHashCode = methodByHashCode.Count != 0 ? methodByHashCode : null;
    }

    public override string ToString()
    {
        var sMethods = "[]";
        if (MethodByFullName != null) {
            var methods = MethodByFullName
                .OrderBy(x => x.Key)
                .Select(x => $"{Environment.NewLine}  {x.Key} -> {x.Value}");
            sMethods = "[" + string.Join("", methods) + Environment.NewLine + "]";
        }
        return $"{nameof(RpcMethodResolver)}(Versions = \"{Versions}\", {nameof(MethodByRef)} = {sMethods})";
    }

    // Nested type

    public readonly record struct MethodEntry(RpcMethodDef Method, Version Version) : ICanBeNone<MethodEntry>
    {
        public static MethodEntry None => default;

        public bool IsNone => ReferenceEquals(Method, null);

        public override string ToString()
            => IsNone
                ? "n/a"
                : $"{Method} (v{Version.Format()})";
    }
}
