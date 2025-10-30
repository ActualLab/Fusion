using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

public sealed class ComputeMethodDef : MethodDef
{
    public ComputedOptions ComputedOptions { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; } = ComputedOptions.Default;
    public readonly bool IsOfHasDisposableStatusType;
    public readonly ComputeMethodDef? ConsolidationTargetMethod;
    public readonly ComputeMethodDef? ConsolidationSourceMethod;

    public ComputeMethodDef(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo method,
        ComputeServiceInterceptor interceptor,
        ComputeMethodDef? consolidationTargetMethod = null
        ) : base(type, method)
    {
        if (!IsAsyncMethod) {
            IsValid = false;
            return;
        }

        var computedOptions = interceptor.Hub.ComputedOptionsProvider.GetComputedOptions(type, method);
        if (computedOptions is null) {
            IsValid = false;
            return;
        }
        if (computedOptions.IsConsolidating) {
            if (consolidationTargetMethod is null)
                ConsolidationSourceMethod = new ComputeMethodDef(type, method, interceptor, this);
            else
                ConsolidationTargetMethod = consolidationTargetMethod;
        }
        else if (consolidationTargetMethod is not null)
            throw new ArgumentOutOfRangeException(nameof(consolidationTargetMethod));

        IsOfHasDisposableStatusType = typeof(IHasDisposeStatus).IsAssignableFrom(type);
        ComputedOptions = computedOptions;
    }

    public override string ToString()
        => _toStringCached ??= string.Concat(
            GetType().Name,
            "(",
            ConsolidationTargetMethod is not null ? "<c-source>" : "",
            ConsolidationSourceMethod is not null ? "<c-target>" : "",
            FullName,
            ")",
            IsValid ? "" : " - invalid");

    public ComputeMethodFunction CreateComputeMethodFunction(FusionHub hub)
    {
        var functionType = ComputedOptions.IsConsolidating
            ? typeof(ConsolidatingComputeMethodFunction<>)
            : typeof(ComputeMethodFunction<>);
        return (ComputeMethodFunction)functionType
            .MakeGenericType(UnwrappedReturnType)
            .CreateInstance(hub, this);
    }

    public RemoteComputeMethodFunction CreateRemoteComputeMethodFunction(FusionHub hub)
    {
        var functionType = typeof(RemoteComputeMethodFunction<>);
        return (RemoteComputeMethodFunction)functionType
            .MakeGenericType(UnwrappedReturnType)
            .CreateInstance(hub, this);
    }
}
