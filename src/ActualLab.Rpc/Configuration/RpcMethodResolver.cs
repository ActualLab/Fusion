#if NET8_0_OR_GREATER
using System.Collections.Frozen;
using ActualLab.OS;
#endif
using ActualLab.Comparison;
using ActualLab.Internal;

namespace ActualLab.Rpc;

public sealed class RpcMethodResolver
{
    public readonly RpcServiceRegistry ServiceRegistry;
    public readonly VersionSet? Versions;
    public readonly IReadOnlyDictionary<string, MethodEntry>? MethodByFullName;
    public readonly IReadOnlyDictionary<RpcMethodRef, MethodEntry>? MethodByRef;
    public readonly IReadOnlyDictionary<int, MethodEntry>? MethodByHashCode;
    public readonly RpcMethodResolver? NextResolver;

    public RpcMethodDef? this[in RpcMethodRef methodRef] {
        get {
            if (MethodByRef is not null && MethodByRef.TryGetValue(methodRef, out var methodEntry))
                return methodEntry.Method;

            return NextResolver?[methodRef];
        }
    }

    public RpcMethodDef? this[string fullName] {
        get {
            if (MethodByFullName is not null && MethodByFullName.TryGetValue(fullName, out var methodEntry))
                return methodEntry.Method;

            return NextResolver?[fullName];
        }
    }

    public RpcMethodDef? this[int hashCode] {
        get {
            if (MethodByHashCode is not null && MethodByHashCode.TryGetValue(hashCode, out var methodEntry))
                return methodEntry.Method;

            return NextResolver?[hashCode];
        }
    }

    public RpcMethodResolver(RpcServiceRegistry serviceRegistry, bool serverOnly, ILogger? log)
    {
        ServiceRegistry = serviceRegistry;
        Versions = null;
        var methodByFullName = new Dictionary<string, MethodEntry>(StringComparer.Ordinal);
        var methodByRef = new Dictionary<RpcMethodRef, MethodEntry>();
        var methodByHashCode = new Dictionary<int, MethodEntry>();
        foreach (var service in ServiceRegistry) {
            if (serverOnly && !service.HasServer)
                continue;

            foreach (var method in service.Methods) {
                var fullName = method.FullName;
                var methodRef = new RpcMethodRef(fullName, method);
                var hashCode = methodRef.HashCode;
                var entry = new MethodEntry(method, VersionExt.MaxValue);
                methodByFullName.Add(method.FullName, entry);
                methodByRef.Add(methodRef, entry);
                methodByHashCode[hashCode] = entry;
            }
        }
        CheckHashCodeConflicts(methodByRef, methodByHashCode, log);
        MethodByFullName = methodByFullName;
        MethodByRef = methodByRef;
        MethodByHashCode = methodByHashCode;
#if NET8_0_OR_GREATER
        if (RuntimeInfo.IsServer) {
            // Uses FrozenDictionary for speed
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
        var methodByFullName = new Dictionary<string, MethodEntry>(StringComparer.Ordinal);
        var methodByHashCode = new Dictionary<int, MethodEntry>();
        foreach (var service in ServiceRegistry) {
            if (!service.HasServer)
                continue; // No need to remap clients

            var scope = service.Scope;
            var version = versions[scope];
            var legacyServiceName = service.LegacyNames[version];
            var serviceName = legacyServiceName?.Name ?? service.Name;
            var serviceVersion = legacyServiceName?.MaxVersion ?? VersionExt.MaxValue;
            foreach (var method in service.Methods) {
                var legacyMethodName = method.LegacyNames[version];
                if (legacyMethodName is null && legacyServiceName is null)
                    continue; // No overrides

                var methodName = legacyMethodName?.Name ?? method.Name;
                var methodVersion = legacyMethodName?.MaxVersion ?? serviceVersion;

                var fullName = RpcMethodDef.ComposeFullName(serviceName, methodName);
                var methodRef = new RpcMethodRef(fullName, method);
                var hashCode = methodRef.HashCode;
                var entry = new MethodEntry(method, methodVersion);
                if (!methodByRef.TryGetValue(methodRef, out var existingEntry)) {
                    // No existing entry -> add a new one
                    methodByRef[methodRef] = entry;
                    methodByFullName[fullName] = entry;
                    methodByHashCode[hashCode] = entry;
                    continue;
                }

                // There is an existing entry -> check version to decide whether to override or not
                var c = methodVersion.CompareTo(existingEntry.Version);
                if (c == 0)
                    throw Errors.Constraint(
                        $"[LegacyName] conflict: '{method.FullName}' and '{existingEntry.Method.FullName}' " +
                        $"are both mapped to '{serviceName}.{methodName}' in v{methodVersion.Format()}.");

                if (c < 0) {
                    // methodVersion < existingEntry.Version -> override.
                    // If there are many Service.Method names w/ the different versions,
                    // we'll take the one with the lowest version:
                    // - method.LegacyNames[version] call above finds the override for this or higher version
                    // - if there are two or more overrides mapped to the same legacy name, the one with
                    //   the lowest version should win, coz it's the one that was added first.
                    methodByRef[methodRef] = entry;
                    methodByFullName[fullName] = entry;
                    methodByHashCode[hashCode] = entry;
                }
            }
        }
        CheckHashCodeConflicts(methodByRef, methodByHashCode, log);
        // Conflicts of legacy names with the non-legacy ones don't matter:
        // we assume legacy method hashes override the non-legacy ones.
        /*
        var conflicts = (
            from kv in methodByRef
            let legacyMethodRef = kv.Key
            let legacyMethodDef = kv.Value.Method
            let version = kv.Value.Version
            let maybeMatch = nextResolver.MethodByHashCode?.GetValueOrDefault(legacyMethodRef.HashCode)
            where maybeMatch.HasValue
            let methodDef = maybeMatch.GetValueOrDefault().Method
            where !ReferenceEquals(legacyMethodDef, methodDef) // Hash match points to a different impl. method
            select (legacyMethodRef, version, methodDef.Ref)
            ).ToList();
        foreach (var (legacyMethodRef, version, methodRef) in conflicts) {
            log?.LogError("RpcMethodRef.HashCode conflict for {LegacyMethodRef} @ {Version} and {MethodRef}",
                legacyMethodRef, version, methodRef);
            methodByHashCode[legacyMethodRef.HashCode] = default;
        }
        */

        MethodByRef = methodByRef.Count != 0 ? methodByRef : null;
        MethodByFullName = methodByFullName.Count != 0 ? methodByFullName : null;
        MethodByHashCode = methodByHashCode.Count != 0 ? methodByHashCode : null;
    }

    public override string ToString()
    {
        var sMethods = "[]";
        if (MethodByFullName is not null) {
            var methods = MethodByFullName
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{Environment.NewLine}  {x.Key} -> {x.Value}");
            sMethods = "[" + string.Join("", methods) + Environment.NewLine + "]";
        }
        return $"{nameof(RpcMethodResolver)}(Versions = \"{Versions}\", {nameof(MethodByRef)} = {sMethods})";
    }

    // Private methods

    private static void CheckHashCodeConflicts(
        Dictionary<RpcMethodRef, MethodEntry> methodByRef,
        Dictionary<int, MethodEntry> methodByHashCode,
        ILogger? log)
    {
        if (methodByHashCode.Count == methodByRef.Count)
            return;

        var conflicts = methodByRef.Keys
            .GroupBy(x => x.HashCode)
            .Where(g => g.Count() > 1)
            .SelectMany(x => x)
            .OrderBy(x => x.HashCode).ThenBy(x => x.FullName, StringComparer.Ordinal)
            .ToList();
        foreach (var methodRef in conflicts) {
            log?.LogError("RpcMethodRef.HashCode conflict for {MethodRef}", methodRef);
            methodByHashCode[methodRef.HashCode] = default;
        }
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
