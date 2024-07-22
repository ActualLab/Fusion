using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

public static class Invalidation
{
    public static bool IsActive
        => (ComputeContext.Current.CallOptions & CallOptions.Invalidate) == CallOptions.Invalidate;

    public static ComputeContextScope Begin()
        => new(ComputeContext.Invalidating);
}
