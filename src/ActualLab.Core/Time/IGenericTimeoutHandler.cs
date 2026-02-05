namespace ActualLab.Time;

/// <summary>
/// Defines a handler that is invoked when a timeout fires.
/// </summary>
public interface IGenericTimeoutHandler
{
    public void OnTimeout(object? argument);
}
