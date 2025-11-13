using ActualLab.Interception;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc;

public partial class RpcMethodDef
{
    public Func<RpcInboundContext, RpcInboundCall> InboundCallFactory { get; protected set; } = null!;
    public Func<RpcPeer, bool>? InboundCallFilter { get; protected set; } = null;
    public Func<RpcInboundCall, Task>[] InboundCallPreprocessors { get; protected set; } = [];
    public Action<RpcInboundCall>? InboundCallValidator { get; protected set; } = null;

    // The delegates and properties below must be initialized in Initialize(),
    // they are supposed to be as efficient as possible (i.e., do less, if possible)
    // taking the values of other properties into account.
    public bool? InboundCallUseFastPipelineInvoker { get; protected set; }
    public Func<ArgumentList, Task> InboundCallServerInvoker { get; protected set; } = null!;
    public Func<RpcInboundCall, Task> InboundCallPipelineInvoker { get; protected set; } = null!;

    public virtual Func<RpcPeer, bool>? CreateInboundCallFilter()
        => IsBackend
            ? peer => peer.Ref.IsBackend
            : null;

    public virtual Func<RpcInboundCall, Task>[] CreateInboundCallPreprocessors()
        => Hub.InboundCallPreprocessors
            .Select(x => x.CreateInboundCallPreprocessor(this))
            .ToArray();

    public virtual Action<RpcInboundCall>? CreateInboundCallValidator()
    {
#if NET6_0_OR_GREATER // NullabilityInfoContext is available in .NET 6.0+
        if (IsSystem || NoWait)
            return null; // These methods are supposed to rely on built-in validation for perf. reasons

        var nonNullableArgIndexesList = new List<int>();
        var nullabilityInfoContext = new NullabilityInfoContext();
        for (var i = 0; i < Parameters.Length; i++) {
            var p = Parameters[i];
            if (p.ParameterType.IsClass && nullabilityInfoContext.Create(p).ReadState == NullabilityState.NotNull)
                nonNullableArgIndexesList.Add(i);
        }
        if (nonNullableArgIndexesList.Count == 0)
            return null;

        var nonNullableArgIndexes = nonNullableArgIndexesList.ToArray();
        return call => {
            var args = call.Arguments!;
            foreach (var index in nonNullableArgIndexes)
                ArgumentNullException.ThrowIfNull(args.GetUntyped(index), Parameters[index].Name);
        };
#else
        return null;
#endif
    }
}
