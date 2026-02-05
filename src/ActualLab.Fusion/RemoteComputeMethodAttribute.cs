namespace ActualLab.Fusion;

#pragma warning disable CA1813 // Consider making sealed

/// <summary>
/// Marks a method as a remote compute method, extending <see cref="ComputeMethodAttribute"/>
/// with remote computed caching configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class RemoteComputeMethodAttribute : ComputeMethodAttribute
{
    /// <summary>
    /// Remote computed caching behavior.
    /// <code>null</code> means "use default".
    /// </summary>
    public RemoteComputedCacheMode CacheMode { get; set; }
}
