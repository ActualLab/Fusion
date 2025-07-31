using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;
using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

public abstract partial class ComputedStateComponent
{
    private static readonly ConcurrentDictionary<Type, string> StateCategoryCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<Type, IComputedStateOptions> StateOptionsCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
    private static readonly ConcurrentDictionary<Type, Func<Type, IComputedStateOptions>> CreateDefaultStateOptionsCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public static ComputedStateComponentOptions DefaultOptions { get; set; }
        = ComputedStateComponentOptions.RecomputeStateOnParameterChange
        | ComputedStateComponentOptions.UseAllRenderPoints;

    public static Func<Type, IComputedStateOptions> DefaultStateOptionsFactory { get; set; } = CreateDefaultStateOptions;

    public static ComputedState<T>.Options GetStateOptions<T>(
        Type componentType, Func<Type, ComputedState<T>.Options>? optionsFactory = null)
        => (ComputedState<T>.Options)StateOptionsCache.GetOrAdd(componentType,
            optionsFactory as Func<Type, IComputedStateOptions> ?? DefaultStateOptionsFactory);

    public static IComputedStateOptions GetStateOptions(
        Type componentType, Func<Type, IComputedStateOptions>? optionsFactory = null)
        => StateOptionsCache.GetOrAdd(componentType, optionsFactory ?? DefaultStateOptionsFactory);

    public static string GetStateCategory(Type componentType)
        => StateCategoryCache.GetOrAdd(componentType, static t => $"{t.GetName()}.State");

    public static string GetMutableStateCategory(Type componentType)
        => StateCategoryCache.GetOrAdd(componentType, static t => $"{t.GetName()}.MutableState");

    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume GetDefaultOptions method is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume GetDefaultOptions method is preserved")]
    public static IComputedStateOptions CreateDefaultStateOptions(Type componentType)
        => CreateDefaultStateOptionsCache.GetOrAdd(componentType,
            static componentType => {
                var type = componentType;
                while (type is not null) {
                    if (type.IsGenericType
                        && type.GetGenericTypeDefinition() is var gtd
                        && gtd == typeof(ComputedStateComponent<>)) {
                        var stateType = type.GetGenericArguments().Single();
                        return GenericInstanceCache
                            .Get<Func<Type, IComputedStateOptions>>(typeof(CreateDefaultStateOptionsFactory<>), stateType)
                            .Invoke(componentType);
                    }
                    type = type.BaseType;
                }
                throw new ArgumentOutOfRangeException(nameof(componentType));
            }).Invoke(componentType);

    // Nested types

    public sealed class CreateDefaultStateOptionsFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (Type componentType) => (IComputedStateOptions)new ComputedState<T>.Options() {
                Category = GetStateCategory(componentType),
            };
    }
}
