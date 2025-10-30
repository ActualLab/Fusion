using ActualLab.Fusion.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Fusion.Interception;

public sealed class ConsolidatingComputeMethodFunction<T>(FusionHub hub, ComputeMethodDef methodDef)
    : ConsolidatingComputeMethodFunction(
        hub, methodDef,
        methodDef.ConsolidationSourceMethodDef ?? throw new ArgumentOutOfRangeException(nameof(methodDef)),
        new ComputeMethodFunction<T>(hub, methodDef.ConsolidationSourceMethodDef!))
{
    protected override Computed NewComputed(ComputeMethodInput input)
        => throw Errors.InternalError($"This method should never be called in {GetType().GetName()}.");

    protected override Computed NewConsolidatingComputed(ComputeMethodInput input, Computed source)
        => new ConsolidatingComputed<T>(ComputedOptions, input, (Computed<T>)source);
}

public abstract class ConsolidatingComputeMethodFunction(
    FusionHub hub,
    ComputeMethodDef methodDef,
    ComputeMethodDef consolidationSourceMethodDef,
    ComputeMethodFunction consolidationSourceFunction
    ) : ComputeMethodFunction(hub, methodDef)
{
    public readonly ComputeMethodDef ConsolidationSourceMethodDef = consolidationSourceMethodDef;
    public readonly ComputeMethodFunction ConsolidationSourceFunction = consolidationSourceFunction;

    protected internal override async ValueTask<Computed> ProduceComputedImpl(
        ComputedInput input, Computed? existing, CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        using var _ = Computed.BeginIsolation();
        var consolidationSourceInput = new ComputeMethodInput(
            ConsolidationSourceFunction,
            ConsolidationSourceMethodDef,
            typedInput.Invocation);
        var computed = await consolidationSourceInput
            .GetOrProduceComputed(ComputeContext.None, cancellationToken)
            .ConfigureAwait(false);
        return NewConsolidatingComputed(typedInput, computed!);
    }

    protected abstract Computed NewConsolidatingComputed(ComputeMethodInput input, Computed source);
}
