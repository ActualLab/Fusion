namespace ActualLab.Interception;

/// <summary>
/// Represents a generated proxy object, which can be bound
/// to an <see cref="InterceptorBinding"/> just once, right after its construction.
/// </summary>
public interface IProxy : IRequiresAsyncProxy
{
    public ProxyMethodTable MethodTable { get; }
    public InterceptorBinding Binding { get; set; }
}
