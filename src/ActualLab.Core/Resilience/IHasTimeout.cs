namespace ActualLab.Resilience;

/// <summary>
/// Defines the contract for objects that have an optional timeout.
/// </summary>
public interface IHasTimeout
{
    public TimeSpan? Timeout { get; }
}
