using ActualLab.Caching;
using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

public abstract partial class ComputedStateComponent
{
    private static readonly ConcurrentDictionary<Type, string> MutableStateCategoryCache
        = new(HardwareInfo.ProcessorCountPo2, 131);
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

    // Invoked once per component type, on a GetStateOptions(Type, factory) cache miss with a non-null factory.
    // The default one runs ValidateStateOptionsFactory only if the factory comes from a Debug build; null disables it.
    public static Action<Type, Func<Type, IComputedStateOptions>>? StateOptionsFactoryValidator { get; set; }
        = static (componentType, optionsFactory) => {
            if (optionsFactory.Method.Module.Assembly.IsDebugBuild())
                ValidateStateOptionsFactory(componentType, optionsFactory);
        };

    // The result is cached per component type, so optionsFactory must not read the component instance
    public static ComputedState<T>.Options GetStateOptions<T>(
        Type componentType, Func<Type, ComputedState<T>.Options>? optionsFactory = null)
        => (ComputedState<T>.Options)GetStateOptions(componentType,
            (Func<Type, IComputedStateOptions>?)optionsFactory);

    // The result is cached per component type, so optionsFactory must not read the component instance
    public static IComputedStateOptions GetStateOptions(
        Type componentType, Func<Type, IComputedStateOptions>? optionsFactory = null)
    {
        if (StateOptionsCache.TryGetValue(componentType, out var options))
            return options;

        if (optionsFactory is null)
            optionsFactory = DefaultStateOptionsFactory;
        else
            StateOptionsFactoryValidator?.Invoke(componentType, optionsFactory);
        return StateOptionsCache.GetOrAdd(componentType, optionsFactory);
    }

    public static void ValidateStateOptionsFactory(
        Type componentType, Func<Type, IComputedStateOptions> optionsFactory)
    {
        if (optionsFactory.IsCaptureFree())
            return;

        throw new InvalidOperationException(
            $"{componentType.GetName()}: the options factory passed to GetStateOptions(Type, ...) captures "
            + $"{optionsFactory.Target!.GetType().GetName()}, but its result is cached per component type "
            + "and shared by all its instances. Make the factory static, or build per-instance options "
            + "with `new() { ... }` instead.");
    }

    public static string GetStateCategory(Type componentType)
        => StateCategoryCache.GetOrAdd(componentType, static t => $"{t.GetName()}.State");

    public static string GetMutableStateCategory(Type componentType)
        => MutableStateCategoryCache.GetOrAdd(componentType, static t => $"{t.GetName()}.MutableState");

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

    /// <summary>
    /// Factory that creates default <see cref="ComputedState{T}.Options"/>
    /// for a given component type.
    /// </summary>
    public sealed class CreateDefaultStateOptionsFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public override object Generate()
            => (Type componentType) => (IComputedStateOptions)new ComputedState<T>.Options() {
                Category = GetStateCategory(componentType),
            };
    }
}
