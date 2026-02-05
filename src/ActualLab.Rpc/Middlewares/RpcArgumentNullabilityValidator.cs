using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Middlewares;

/// <summary>
/// An <see cref="IRpcMiddleware"/> that validates non-nullable reference-type arguments on inbound RPC calls.
/// </summary>
public sealed record RpcArgumentNullabilityValidator : IRpcMiddleware
{
    public static Func<RpcMethodDef, bool> DefaultFilter { get; set; } = _ => RuntimeInfo.IsServer;

    public double Priority { get; init; } = RpcInboundMiddlewarePriority.ArgumentValidation;
    public Func<RpcMethodDef, bool> Filter { get; init; } = DefaultFilter;

    public Func<RpcInboundCall, Task<T>> Create<T>(RpcMiddlewareContext<T> context, Func<RpcInboundCall, Task<T>> next)
    {
#if !NET6_0_OR_GREATER
        // NullabilityInfoContext is available in .NET 6.0+
        return next;
#else
        // System and NoWait methods must rely on built-in validation for perf. reasons
        var methodDef = context.MethodDef;
        if (methodDef.IsSystem || methodDef.NoWait || !Filter.Invoke(methodDef))
            return next;

        var nonNullableArgIndexesList = new List<int>();
        var nullabilityInfoContext = new NullabilityInfoContext();
        var parameters = methodDef.Parameters;
        for (var i = 0; i < parameters.Length; i++) {
            var p = parameters[i];
            if (!p.ParameterType.IsValueType && nullabilityInfoContext.Create(p).ReadState == NullabilityState.NotNull)
                nonNullableArgIndexesList.Add(i);
        }
        if (nonNullableArgIndexesList.Count == 0)
            return next;

        var nonNullableArgIndexes = nonNullableArgIndexesList.ToArray();
        var index0 = nonNullableArgIndexes.GetValueOrDefault(0);
        var index1 = nonNullableArgIndexes.GetValueOrDefault(1);
        var p0Name = parameters.GetValueOrDefault(index0)?.Name;
        var p1Name = parameters.GetValueOrDefault(index1)?.Name;
        return nonNullableArgIndexes.Length switch {
            1 => call => {
                ArgumentNullException.ThrowIfNull(call.Arguments!.GetUntyped(index0), p0Name);
                return next.Invoke(call);
            },
            2 => call => {
                var args = call.Arguments!;
                ArgumentNullException.ThrowIfNull(args.GetUntyped(index0), p0Name);
                ArgumentNullException.ThrowIfNull(args.GetUntyped(index1), p1Name);
                return next.Invoke(call);
            },
            _ => call => {
                var args = call.Arguments!;
                foreach (var index in nonNullableArgIndexes)
                    ArgumentNullException.ThrowIfNull(args.GetUntyped(index), parameters[index].Name);
                return next.Invoke(call);
            },
        };
#endif
    }
}
