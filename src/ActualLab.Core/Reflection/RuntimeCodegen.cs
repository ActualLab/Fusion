using System.Reflection.Emit;

namespace ActualLab.Reflection;

/// <summary>
/// Defines runtime code generation strategy values.
/// </summary>
public enum RuntimeCodegenMode
{
    DynamicMethods = 0,
    InterpretedExpressions = 1,
    CompiledExpressions = 3,
}

/// <summary>
/// Detects and controls the runtime code generation mode (dynamic methods vs.
/// expression trees).
/// </summary>
public static class RuntimeCodegen
{
    private static readonly LazySlim<RuntimeCodegenMode> DefaultModeLazy = new(static () => {
#if NETSTANDARD2_0
        try {
            _ = new DynamicMethod("_IsDynamicCodeSupported", typeof(void), []);
            return RuntimeCodegenMode.DynamicMethods;
        }
        catch {
            return RuntimeCodegenMode.InterpretedExpressions;
        }
#else
        return RuntimeFeature.IsDynamicCodeSupported
            ? RuntimeCodegenMode.DynamicMethods
            : RuntimeCodegenMode.InterpretedExpressions;
#endif
    });

    public static RuntimeCodegenMode NativeMode => DefaultModeLazy.Value;
    public static RuntimeCodegenMode Mode { get; set; }
#if USE_DYNAMIC_METHODS
        = NativeMode;
#else
        = RuntimeCodegenMode.InterpretedExpressions;
#endif
}
