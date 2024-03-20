using ActualLab.Versioning;

namespace ActualLab.Fusion.Interception;

public class ComputeMethodFunction<T>(
    ComputeMethodDef methodDef,
    IServiceProvider services
    ) : ComputeMethodFunctionBase<T>(methodDef, services)
{
    protected override Computed<T> CreateComputed(ComputeMethodInput input)
        => new ComputeMethodComputed<T>(ComputedOptions, input);
}
