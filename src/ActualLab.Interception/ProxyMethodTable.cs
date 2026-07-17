namespace ActualLab.Interception;

/// <summary>
/// An immutable, per-proxy-type table of intercepted methods.
/// Each method is identified by its slot index in <see cref="Methods"/>;
/// slot indexes are local to their table and must always be paired with it.
/// </summary>
public sealed class ProxyMethodTable
{
    private readonly Dictionary<MethodInfo, int> _indexes;
    // Caches GetIndex results for alias MethodInfo-s - identities that differ from
    // the effective ones in Methods (base-class declarations of inherited/overridden
    // methods, equivalent declarations from other interfaces), incl. -1 for foreign methods
    private readonly ConcurrentDictionary<MethodInfo, int> _aliasIndexes = new();

    public readonly Type ProxyType;
    public readonly Type ServiceType;
    public readonly MethodInfo[] Methods;

    public int Length {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Methods.Length;
    }

    public MethodInfo this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Methods[index];
    }

    // No range check: the index must originate from an Invocation, which validates
    // it against this table on construction. Method resolution can be frequent
    // (e.g. a handler reading invocation.Method on every call), so it relies
    // on that one-time check instead of per-access ones.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodInfo GetMethodUnchecked(int index)
#if NET6_0_OR_GREATER
        => Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Methods), index);
#else
        => Methods[index];
#endif

    public ProxyMethodTable(Type proxyType, MethodInfo[] methods)
    {
        ProxyType = proxyType;
        ServiceType = proxyType.NonProxyType();
        Methods = methods;
        _indexes = new Dictionary<MethodInfo, int>(methods.Length);
        for (var i = 0; i < methods.Length; i++)
            _indexes[methods[i]] = i;
    }

    public override string ToString()
        => $"{nameof(ProxyMethodTable)}({ProxyType.GetName()}, {Methods.Length} method(s))";

    // Returns -1 if the method doesn't belong to this table.
    // This is a cold-path API: generated code always knows its slot indexes.
    public int GetIndex(MethodInfo method)
        => _indexes.TryGetValue(method, out var index)
            ? index
            : _aliasIndexes.GetOrAdd(method, static (method1, self) => self.FindCompatibleIndex(method1), this);

    // Private methods

    private int FindCompatibleIndex(MethodInfo method)
    {
        // Resolves aliases: base-class declarations of overridden methods and
        // equivalent declarations from different interfaces map to one effective slot.
        var name = method.Name;
        var parameters = method.GetParameters();
        for (var i = 0; i < Methods.Length; i++) {
            var m = Methods[i];
            if (!string.Equals(m.Name, name, StringComparison.Ordinal))
                continue;
            if (m.ReturnType != method.ReturnType)
                continue;

            var mParameters = m.GetParameters();
            if (mParameters.Length != parameters.Length)
                continue;

            var isMatch = true;
            for (var j = 0; j < parameters.Length; j++) {
                if (mParameters[j].ParameterType != parameters[j].ParameterType) {
                    isMatch = false;
                    break;
                }
            }
            if (isMatch)
                return i;
        }
        return -1;
    }
}
