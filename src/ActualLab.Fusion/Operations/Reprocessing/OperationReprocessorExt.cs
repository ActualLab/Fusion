using ActualLab.Resilience;

namespace ActualLab.Fusion.Operations.Reprocessing;

/// <summary>
/// Extension methods for <see cref="IOperationReprocessor"/>.
/// </summary>
public static class OperationReprocessorExt
{
    public static bool WillRetry(this IOperationReprocessor reprocessor, Exception error, out Transiency transiency)
        => reprocessor.WillRetry(error.Flatten(), out transiency);
}
