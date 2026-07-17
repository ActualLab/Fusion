using ActualLab.Interception.Internal;

namespace ActualLab.Interception;

/// <summary>
/// Binds an <see cref="Interception.Interceptor"/> to a <see cref="ProxyMethodTable"/> and caches
/// the handler resolved for each method slot, so a handler is resolved at most once
/// per (interceptor, method table, slot). One binding exists per such pair; it is
/// shared by all proxy instances using it, which cache resolved handlers in their
/// own per-slot fields.
/// </summary>
public sealed class InterceptorBinding
{
    // The resolved state of a slot the interceptor leaves unhandled. Generated proxies
    // reference-compare against it to invoke their typed original-call delegate directly
    // (avoiding boxed results); it is still invocable for non-generated callers.
    public static readonly Func<Invocation, object?> NoHandler =
        static invocation => invocation.InvokeInterceptedUntyped();

    // Per-slot resolved handlers; null = unresolved, so a resolved handler
    // (including NoHandler) is never null.
    private readonly Func<Invocation, object?>?[] _handlers;

    public readonly Interceptor Interceptor;
    public readonly ProxyMethodTable MethodTable;

    internal InterceptorBinding(Interceptor interceptor, ProxyMethodTable methodTable)
    {
        Interceptor = interceptor;
        MethodTable = methodTable;
        _handlers = new Func<Invocation, object?>?[methodTable.Length];
    }

    public override string ToString()
        => $"{nameof(InterceptorBinding)}({Interceptor} -> {MethodTable})";

    // The slow path of generated proxy methods: resolves the handler and caches it
    // in the proxy's per-slot field. The proxy's binding is assigned once right after
    // construction and never changes, and racing threads store the same handler
    // instance here (GetHandler returns the single published one), so plain,
    // read-optimized field access is safe on both sides.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Func<Invocation, object?> GetAndCacheHandler(
        ref Func<Invocation, object?>? handlerSlot,
        InterceptorBinding? binding,
        in Invocation invocation)
    {
        if (binding is null)
            throw Errors.NoInterceptor();

        var handler = binding.GetHandler(invocation);
        handlerSlot = handler;
        return handler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Func<Invocation, object?> GetHandler(in Invocation invocation)
        => _handlers[invocation.MethodIndex] ?? ResolveHandler(invocation);

    // Private methods

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Func<Invocation, object?> ResolveHandler(in Invocation invocation)
    {
        // SelectHandler runs at most once per slot; concurrent first calls may
        // create duplicate candidates, but only the first published one is ever used.
        var handler = Interceptor.SelectHandler(invocation) ?? NoHandler;
        return Interlocked.CompareExchange(ref _handlers[invocation.MethodIndex], handler, null) ?? handler;
    }
}
