using ActualLab.Fusion.Internal;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

public interface IComputeMethodFunction : IComputeFunction
{
    ComputeMethodDef MethodDef { get; }
    ComputedOptions ComputedOptions { get; }
}

public class ComputeMethodFunction<T>(
    ComputeMethodDef methodDef,
    FusionInternalHub hub
    ) : ComputeFunctionBase<T>(hub), IComputeMethodFunction
{
    ComputeMethodDef IComputeMethodFunction.MethodDef => MethodDef;
    ComputedOptions IComputeMethodFunction.ComputedOptions => ComputedOptions;

    public readonly ComputeMethodDef MethodDef = methodDef;
    public readonly ComputedOptions ComputedOptions = methodDef.ComputedOptions;

    public override string ToString()
        => MethodDef.FullName;

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
                var result = InvokeImplementation(typedInput, cancellationToken);
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
    protected static object InvokeImplementation(ComputeMethodInput input, CancellationToken cancellationToken)
    {
        var ctIndex = input.MethodDef.CancellationTokenIndex;
        var invocation = input.Invocation;
        if (ctIndex < 0)
            return invocation.InterceptedUntyped()!;

        var arguments = invocation.Arguments;
        arguments.SetCancellationToken(ctIndex, cancellationToken);
        try {
            return invocation.InterceptedUntyped()!;
        }
        finally {
            arguments.SetCancellationToken(ctIndex, default); // Otherwise it may cause memory leak
        }
    }
}
