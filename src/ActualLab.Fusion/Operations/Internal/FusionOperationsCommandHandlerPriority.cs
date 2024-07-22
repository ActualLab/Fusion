namespace ActualLab.Fusion.Operations.Internal;

public static class FusionOperationsCommandHandlerPriority
{
    public const double OperationReprocessor = 100_000;
    public const double NestedCommandLogger = 11_000;
    public const double InMemoryOperationScopeProvider = 10_000;
    public const double ComputeServiceCommandCompletionInvalidator = 100;
    public const double CompletionTerminator = -1000_000_000;
}
