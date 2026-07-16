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
#if NET9_0_OR_GREATER
        var lookup = new ComputeMethodInput.Lookup(this, MethodDef, invocation);
#else
        var input = new ComputeMethodInput(this, MethodDef, invocation);
#endif
        var cancellationToken = invocation.Arguments.GetCancellationToken(CancellationTokenIndex); // Auto-handles -1 index
        try {
            var context = ComputeContext.Current;
#if NET9_0_OR_GREATER
            // ComputeMethodInput is sealed, and its GetExistingComputed() performs the same registry lookup.
            var computed = ComputedRegistry.Get(lookup);
#else
            var computed = input.GetExistingComputed();
#endif
            if ((context.CallOptions & CallOptions.Invalidate) == CallOptions.Invalidate) {
                _ = ComputedImpl.TryUseExisting(computed, context);
                return MethodDef.DefaultResult;
            }

            var task = ComputedImpl.TryUseExisting(computed, context)
                ? ComputedImpl.GetValueOrDefaultAsTask(computed, context, OutputType)
#if NET9_0_OR_GREATER
                : this.ProduceValuePromise(lookup.ToInput(), context, cancellationToken);
#else
                : this.ProduceValuePromise(input, context, cancellationToken);
#endif
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
