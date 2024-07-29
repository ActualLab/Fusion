using System.Reflection.Emit;

namespace ActualLab.Reflection;

public enum RuntimeCodegenMode
{
    DynamicMethods = 0,
    InterpretedExpressions = 1,
    CompiledExpressions = 3,
}

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
