namespace ActualLab.Fusion.Interception;

public interface IComputeFunction : IFunction
{
    ComputeMethodDef MethodDef { get; }
    ComputedOptions ComputedOptions { get; }
}

public abstract class ComputeFunctionBase<T>(ComputeMethodDef methodDef, IServiceProvider services)
    : FunctionBase<T>(services), IComputeFunction
{
    public ComputeMethodDef MethodDef { get; } = methodDef;
    public ComputedOptions ComputedOptions { get; } = methodDef.ComputedOptions;

    public override string ToString()
        => MethodDef.FullName;
}
