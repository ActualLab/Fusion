using ActualLab.Fusion.Internal;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

public sealed class ComputeMethodFunction<T>(FusionHub hub, ComputeMethodDef methodDef)
    : ComputeMethodFunction(hub, methodDef)
{
    protected override Computed NewComputed(ComputeMethodInput input)
        => new ComputeMethodComputed<T>(ComputedOptions, input);
}

public abstract class ComputeMethodFunction : ComputeFunction
{
    public readonly ComputeMethodDef MethodDef;
    public readonly ComputedOptions ComputedOptions;
    public readonly int CancellationTokenIndex;

    protected ComputeMethodFunction(FusionHub hub, ComputeMethodDef methodDef) : base(hub, methodDef.UnwrappedReturnType)
    {
        if (methodDef.ComputedOptions.IsConsolidating)
            throw new ArgumentOutOfRangeException(nameof(methodDef));

        MethodDef = methodDef;
        ComputedOptions = methodDef.ComputedOptions;
        CancellationTokenIndex = methodDef.CancellationTokenIndex;
    }

    public override string ToString()
        => MethodDef.FullName;

    public object? ComputeServiceInterceptorHandler(Invocation invocation)
    {
        var input = new ComputeMethodInput(this, MethodDef, invocation);
        var cancellationToken = invocation.Arguments.GetCancellationToken(CancellationTokenIndex); // Auto-handles -1 index
        try {
            var task = input.GetOrProduceValuePromise(ComputeContext.Current, cancellationToken);
            return MethodDef.WrapAsyncInvokerResultOfAsyncMethodUntyped(task);
        }
        finally {
            if (cancellationToken.CanBeCanceled)
                // ComputedInput is stored in ComputeRegistry, so we remove CancellationToken there
                // to prevent memory leaks + possible unexpected cancellations on .Update calls.
                invocation.Arguments.SetCancellationToken(CancellationTokenIndex, default);
        }
    }

    protected override async ValueTask<Computed> ProduceComputedImpl(
        ComputedInput input, Computed? existing, CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            var computed = NewComputed(typedInput);
            try {
                using var _ = Computed.BeginCompute(computed);
                var result = await typedInput.InvokeInterceptedUntyped(cancellationToken).ConfigureAwait(false);
                computed.TrySetValue(result);
                return computed;
            }
            catch (Exception e) {
                var delayTask = ComputedImpl.FinalizeAndTryReprocessInternalCancellation(
                    nameof(ProduceComputedImpl), computed, e, startedAt, ref tryIndex, Log, cancellationToken);
                if (delayTask == SpecialTasks.MustThrow)
                    throw;
                if (delayTask == SpecialTasks.MustReturn)
                    return computed;
                await delayTask.ConfigureAwait(false);
            }
        }
    }

    protected abstract Computed NewComputed(ComputeMethodInput input);
}
