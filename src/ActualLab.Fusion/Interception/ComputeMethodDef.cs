using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Interception;

/// <summary>
/// Describes a compute method, including its <see cref="ComputedOptions"/>
/// and optional consolidation configuration.
/// </summary>
public sealed class ComputeMethodDef : MethodDef
{
    private static readonly ConcurrentDictionary<Type, object> ConsolidationComparerCache = new();

    public ComputedOptions ComputedOptions { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; init; } = ComputedOptions.Default;
    public readonly bool IsOfHasDisposableStatusType;
    public readonly ComputeMethodDef? ConsolidationSourceMethodDef;
    public readonly ComputeMethodDef? ConsolidationTargetMethodDef;
    public readonly object? ConsolidationComparer;

    public ComputeMethodDef(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo methodInfo,
        ComputeServiceInterceptor interceptor,
        ComputeMethodDef? consolidationTargetMethodDef = null
        ) : base(type, methodInfo)
    {
        if (!IsAsyncMethod) {
            IsValid = false;
            return;
        }

        var computedOptions = interceptor.Hub.ComputedOptionsProvider.GetComputedOptions(type, methodInfo);
        if (computedOptions is null) {
            IsValid = false;
            return;
        }
        if (computedOptions.IsConsolidating) {
            if (consolidationTargetMethodDef is null) {
                // We are the consolidation target, the twin we create is going to be the consolidation source
                ConsolidationSourceMethodDef = new ComputeMethodDef(type, methodInfo, interceptor, this);
                ConsolidationTargetMethodDef = null;
                computedOptions = computedOptions with {
                    // All invalidation delays are disabled for the consolidation target
                    InvalidationDelay = TimeSpan.Zero,
                    AutoInvalidationDelay = TimeSpanExt.Infinite,
                    TransientErrorInvalidationDelay = TimeSpanExt.Infinite,
                    // Cancellation reprocessing should happen only for the consolidation source
                    CancellationReprocessing = ComputedCancellationReprocessingOptions.None,
                };
            }
            else { // We are the consolidation source
                ConsolidationSourceMethodDef = null;
                ConsolidationTargetMethodDef = consolidationTargetMethodDef;
                computedOptions = computedOptions with {
                    // Consolidation delay is disabled for the consolidation source
                    ConsolidationDelay = TimeSpanExt.Infinite,
                    // Min cache duration is applied to the consolidation target -
                    // it references the consolidation source.
                    // Moreover, the source is updated more frequently than the target,
                    // so it's reasonable to make source's updates cheaper.
                    MinCacheDuration = TimeSpan.Zero,
                };
            }
        }
        else if (consolidationTargetMethodDef is not null)
            throw new ArgumentOutOfRangeException(nameof(consolidationTargetMethodDef));

        IsOfHasDisposableStatusType = typeof(IHasDisposeStatus).IsAssignableFrom(type);
        ComputedOptions = computedOptions;
        ConsolidationComparer = GetConsolidationComparer(
            computedOptions.ConsolidationComparerType, UnwrappedReturnType, methodInfo);
    }

    public override string ToString()
        => _toStringCached ??= string.Concat(
            nameof(ComputeMethodDef),
            "(",
            ConsolidationSourceMethodDef is not null ? "<consolidating>" : "",
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

    public RemoteComputeMethodFunction CreateRemoteComputeMethodFunction(FusionHub hub, RpcMethodDef rpcMethodDef)
    {
        var functionType = typeof(RemoteComputeMethodFunction<>);
        return (RemoteComputeMethodFunction)functionType
            .MakeGenericType(UnwrappedReturnType)
            .CreateInstance(hub, this, rpcMethodDef);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067",
        Justification = "ConsolidationComparerType is annotated with PublicConstructors")]
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "We assume attributes on compute methods are fully preserved")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "We assume attributes on compute methods are fully preserved")]
    public static object? GetConsolidationComparer(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type? comparerType,
        Type valueType,
        MethodInfo methodInfo)
    {
        if (comparerType is null)
            return null;

        if (!typeof(IEqualityComparer<>).MakeGenericType(valueType).IsAssignableFrom(comparerType))
            throw Errors.ConsolidationComparerMustImplementIEqualityComparer(comparerType, valueType, methodInfo);
        if (comparerType.IsAbstract || comparerType.GetConstructor(Type.EmptyTypes) is null)
            throw Errors.ConsolidationComparerMustHaveParameterlessConstructor(comparerType, methodInfo);

        return ConsolidationComparerCache.GetOrAdd(comparerType, static t => t.CreateInstance());
    }
}
