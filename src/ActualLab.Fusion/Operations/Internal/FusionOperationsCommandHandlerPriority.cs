namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Defines command handler priority constants for Fusion operations pipeline handlers.
/// </summary>
public static class FusionOperationsCommandHandlerPriority
{
    public const double OperationReprocessor = 100_000;
    public const double NestedCommandLogger = 11_000;
    public const double InMemoryOperationScopeProvider = 10_000;
    public const double InvalidatingCommandCompletionHandler = 100;
    public const double CompletionTerminator = -1000_000_000;
}
