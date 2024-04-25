namespace ActualLab.Fusion;

public static class InvalidationMode
{
    public static bool IsOn
        => ComputeContext.Current.IsInvalidating;

    public static ComputeContextScope Begin()
        => new(ComputeContext.Invalidation);
}
