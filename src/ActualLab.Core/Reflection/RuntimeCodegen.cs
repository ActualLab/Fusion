namespace ActualLab.Reflection;

public enum RuntimeCodegenMode
{
    DynamicMethods = 0,
    InterpretedExpressions = 1,
    CompiledExpressions = 3,
}

public static class RuntimeCodegen
{
    public static RuntimeCodegenMode Mode { get; set; }
#if USE_DYNAMIC_METHODS
        = RuntimeFeature.IsDynamicCodeSupported
            ? RuntimeCodegenMode.DynamicMethods
            : RuntimeCodegenMode.InterpretedExpressions;
#else
        = RuntimeCodegenMode.InterpretedExpressions;
#endif
}
