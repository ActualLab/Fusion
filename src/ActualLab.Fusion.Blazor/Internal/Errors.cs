namespace ActualLab.Fusion.Blazor.Internal;

/// <summary>
/// Factory methods for Fusion.Blazor-specific exceptions.
/// </summary>
public static class Errors
{
    public static Exception StateOptionsFactoryCapturesState(Type componentType, Type capturedType)
        => new InvalidOperationException(
            $"{componentType.GetName()}: the options factory passed to GetStateOptions(Type, ...) captures "
            + $"{capturedType.GetName()}, but its result is cached per component type "
            + "and shared by all its instances. Make the factory static, or build per-instance options "
            + "with `new() { ... }` instead.");

    public static Exception CreateStateIsNotOverridden()
        => new NotImplementedException(
            "CreateState is called when SetParametersAsync doesn't call SetState. " +
            "You must override either this method, OnInitialized, or SetParametersAsync to make sure State is set.");
}
