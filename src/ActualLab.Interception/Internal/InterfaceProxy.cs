namespace ActualLab.Interception.Internal;

/// <summary>
/// Base class for generated interface proxies that delegate calls to a target object.
/// </summary>
public abstract class InterfaceProxy
{
    public object? ProxyTarget { get; set; }
}
