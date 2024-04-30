using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public static class Invalidation
{
    public static bool IsActive
        => ComputeContext.Current.IsInvalidating;

    public static ComputeContextScope Begin()
        => new(ComputeContext.Invalidation);
}
