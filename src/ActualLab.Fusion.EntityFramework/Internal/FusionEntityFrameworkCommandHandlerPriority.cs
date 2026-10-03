namespace ActualLab.Fusion.EntityFramework.Internal;

/// <summary>
/// Defines command handler priority constants for Fusion EntityFramework integration.
/// </summary>
public static class FusionEntityFrameworkCommandHandlerPriority
{
    // Between TransientOperationScopeProvider (10_000) and DeferredInvalidationScopeProvider (9000)
    public const double DbOperationScopeProvider = 9900;
}
