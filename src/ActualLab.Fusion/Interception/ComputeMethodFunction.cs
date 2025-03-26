using ActualLab.Fusion.Internal;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

public interface IComputeMethodFunction : IComputeFunction
{
    ComputeMethodDef MethodDef { get; }

    object? ComputeServiceInterceptorHandler(Invocation invocation);
}

public class ComputeMethodFunction<T>(FusionHub hub, ComputeMethodDef methodDef)
    : ComputeFunctionBase<T>(hub, methodDef.UnwrappedReturnType), IComputeMethodFunction
{
    public readonly ComputeMethodDef MethodDef = methodDef;
    public readonly ComputedOptions ComputedOptions = methodDef.ComputedOptions;
    public readonly int CancellationTokenIndex = methodDef.CancellationTokenIndex;

    // IComputeMethodFunction implementation
    ComputeMethodDef IComputeMethodFunction.MethodDef => MethodDef;

    public override string ToString()
        => MethodDef.FullName;

    public object? ComputeServiceInterceptorHandler(Invocation invocation)
    {
        var input = new ComputeMethodInput(this, MethodDef, invocation);
        var cancellationToken = invocation.Arguments.GetCancellationToken(CancellationTokenIndex); // Auto-handles -1 index
        try {
            // Inlined:
            // var task = function.InvokeAndStrip(input, ComputeContext.Current, cancellationToken);
            var context = ComputeContext.Current;
            var computed = ComputedRegistry.Instance.Get(input) as Computed<T>; // = input.GetExistingComputed()
            var task = ComputedImpl.TryUseExisting(computed, context)
                ? ComputedImpl.StripToTask(computed, context)
                : TryRecompute(input, context, cancellationToken);
            // ReSharper disable once HeapView.BoxingAllocation
            return MethodDef.ReturnsValueTask ? new ValueTask<T>(task) : task;
        }
        finally {
            if (cancellationToken.CanBeCanceled)
                // ComputedInput is stored in ComputeRegistry, so we remove CancellationToken there
                // to prevent memory leaks + possible unexpected cancellations on .Update calls.
                invocation.Arguments.SetCancellationToken(CancellationTokenIndex, default);
        }
    }

    protected override async ValueTask<Computed<T>> Compute(
        ComputedInput input, Computed<T>? existing,
        CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            var computed = new ComputeMethodComputed<T>(ComputedOptions, typedInput);
            try {
                using var _ = Computed.BeginCompute(computed);
                var result = InvokeIntercepted(typedInput, cancellationToken);
                if (typedInput.MethodDef.ReturnsValueTask) {
                    var output = await ((ValueTask<T>)result).ConfigureAwait(false);
                    computed.TrySetOutput(output);
                }
                else {
                    var output = await ((Task<T>)result).ConfigureAwait(false);
                    computed.TrySetOutput(output);
                }
                return computed;
            }
            catch (Exception e) {
                var delayTask = ComputedImpl.FinalizeAndTryReprocessInternalCancellation(
                    nameof(Compute), computed, e, startedAt, ref tryIndex, Log, cancellationToken);
                if (delayTask == SpecialTasks.MustThrow)
                    throw;
                if (delayTask == SpecialTasks.MustReturn)
                    return computed;
                await delayTask.ConfigureAwait(false);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static object InvokeIntercepted(ComputeMethodInput input, CancellationToken cancellationToken)
    {
        var ctIndex = input.MethodDef.CancellationTokenIndex;
        var invocation = input.Invocation;
        if (ctIndex < 0)
            return invocation.InvokeInterceptedUntyped()!;

        var arguments = invocation.Arguments;
        arguments.SetCancellationToken(ctIndex, cancellationToken);
        try {
            return invocation.InvokeInterceptedUntyped()!;
        }
        finally {
            arguments.SetCancellationToken(ctIndex, default); // Otherwise it may cause memory leak
        }
    }
}
