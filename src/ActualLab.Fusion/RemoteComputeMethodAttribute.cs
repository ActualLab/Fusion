namespace ActualLab.Fusion;

#pragma warning disable CA1813 // Consider making sealed

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class RemoteComputeMethodAttribute : ComputeMethodAttribute
{
    /// <summary>
    /// Remote computed caching behavior.
    /// <code>null</code> means "use default".
    /// </summary>
    public RemoteComputedCacheMode CacheMode { get; set; }
}
