using ActualLab.Fusion.Internal;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

/// <summary>
/// A strongly-typed <see cref="ComputeMethodFunction"/> that creates
/// <see cref="ComputeMethodComputed{T}"/> instances.
/// </summary>
public sealed class ComputeMethodFunction<T> : ComputeMethodFunction
{
    public ComputeMethodFunction(FusionHub hub, ComputeMethodDef methodDef) : base(hub, methodDef)
    {
        if (methodDef.ComputedOptions.IsConsolidating)
            throw new ArgumentOutOfRangeException(nameof(methodDef));
    }

    protected override Computed NewComputed(ComputeMethodInput input)
        => new ComputeMethodComputed<T>(ComputedOptions, input);
}

/// <summary>
/// A <see cref="ComputeFunction"/> that handles compute method interception,
/// producing <see cref="Computed"/> instances for intercepted method calls.
/// </summary>
public abstract class ComputeMethodFunction(FusionHub hub, ComputeMethodDef methodDef)
    : ComputeFunction(hub, methodDef.UnwrappedReturnType)
{
    public readonly ComputeMethodDef MethodDef = methodDef;
    public readonly ComputedOptions ComputedOptions = methodDef.ComputedOptions;
    public readonly int CancellationTokenIndex = methodDef.CancellationTokenIndex;

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

    protected internal override async ValueTask<Computed> ProduceComputedImpl(
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
