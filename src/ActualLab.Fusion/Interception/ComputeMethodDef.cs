using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

public sealed class ComputeMethodDef : MethodDef
{
    private object? _defaultResult;

    public ComputedOptions ComputedOptions { get; init; } = ComputedOptions.Default;
    public object DefaultResult => _defaultResult ??= GetDefaultResult();

    public readonly bool IsDisposable;

    public ComputeMethodDef(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]Type type,
        MethodInfo method,
        ComputeServiceInterceptorBase interceptor
        ) : base(type, method)
    {
        if (!IsAsyncMethod) {
            IsValid = false;
            return;
        }

        var computedOptions = interceptor.Hub.ComputedOptionsProvider.GetComputedOptions(type, method);
        if (computedOptions == null) {
            IsValid = false;
            return;
        }

        IsDisposable = typeof(IHasIsDisposed).IsAssignableFrom(type);
        ComputedOptions = computedOptions;
    }

    public ComputeMethodInput CreateInput(IFunction function, Invocation invocation)
        => new(function, this, invocation);

    // Private methods

    private object GetDefaultResult()
        => ReturnsValueTask
            ? ValueTaskExt.FromDefaultResult(UnwrappedReturnType)
            : TaskExt.FromDefaultResult(UnwrappedReturnType);
}
