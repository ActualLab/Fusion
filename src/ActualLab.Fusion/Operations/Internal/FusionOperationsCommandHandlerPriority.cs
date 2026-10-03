namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Defines command handler priority constants for Fusion operations pipeline handlers.
/// </summary>
public static class FusionOperationsCommandHandlerPriority
{
    // Between CommanderCommandHandlerPriority's PreparedCommandHandler (1_000_000_000) and
    // CommandTracer (998_000_000): a command that must not run at all shouldn't be traced first
    public const double InvalidationGuard = 999_999_000;
    public const double OperationReprocessor = 100_000;
    public const double TransientOperationScopeProvider = 10_000;
    // Below every operation scope provider (DbOperationScopeProvider is at 9900), so that a scope
    // is in place before any block can be deferred - the mode's carrier has to exist by then
    public const double DeferredInvalidationScopeProvider = 9_000;
    public const double CompletionTerminator = -1000_000_000;
}
