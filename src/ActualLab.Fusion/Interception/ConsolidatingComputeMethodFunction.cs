using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Interception;

public sealed class ConsolidatingComputeMethodFunction<T>(FusionHub hub, ComputeMethodDef methodDef)
    : ConsolidatingComputeMethodFunction(hub, methodDef)
{
    protected override Computed NewComputed(ComputeMethodInput input)
        => new ComputeMethodComputed<T>(ComputedOptions, input);

    protected override Computed NewConsolidatingComputed(ComputeMethodInput input, Computed original)
        => new ConsolidatingComputed<T>(ComputedOptions, input, (Computed<T>)original);
}

public abstract class ConsolidatingComputeMethodFunction : ComputeMethodFunction
{
    protected ConsolidatingComputeMethodFunction(FusionHub hub, ComputeMethodDef methodDef) : base(hub, methodDef)
    {
        if (!methodDef.ComputedOptions.IsConsolidating)
            throw new ArgumentOutOfRangeException(nameof(methodDef));
    }

    protected override ValueTask<Computed> ProduceComputedImpl(
        ComputedInput input, Computed? existing, CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        return typedInput.MethodDef.ConsolidationTargetMethod is { } consolidationTargetMethod
            ? ProduceConsolidatingComputedImpl(typedInput, consolidationTargetMethod, cancellationToken)
            : base.ProduceComputedImpl(input, existing, cancellationToken);
    }

    private async ValueTask<Computed> ProduceConsolidatingComputedImpl(
        ComputeMethodInput input, ComputeMethodDef consolidationTargetMethod, CancellationToken cancellationToken)
    {
        using var _ = Computed.BeginIsolation();
        var consolidationTargetInput = new ComputeMethodInput(
            input.Function, consolidationTargetMethod, input.Invocation);
        var computed = await consolidationTargetInput
            .GetOrProduceComputed(ComputeContext.None, cancellationToken)
            .ConfigureAwait(false);
        return NewConsolidatingComputed(input, computed!);
    }

    protected abstract Computed NewConsolidatingComputed(ComputeMethodInput input, Computed original);
}
