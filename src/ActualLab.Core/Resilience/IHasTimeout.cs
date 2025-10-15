namespace ActualLab.Resilience;

public interface IHasTimeout
{
    public TimeSpan? Timeout { get; }
}
