namespace ActualLab.Interception;

/// <summary>
/// Represents a proxy object that can have an <see cref="Interceptor"/> assigned to it.
/// </summary>
public interface IProxy : IRequiresAsyncProxy
{
    public Interceptor Interceptor { get; set; }
}
