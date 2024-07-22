using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

public sealed class ComputeMethodDef : MethodDef
{
    public ComputedOptions ComputedOptions { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; } = ComputedOptions.Default;
    public readonly bool IsDisposable;

    public ComputeMethodDef(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]Type type,
        MethodInfo method,
        ComputeServiceInterceptor interceptor
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
}
