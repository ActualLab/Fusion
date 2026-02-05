namespace ActualLab.Concurrency;

/// <summary>
/// Indicates the implementing type exposes a <see cref="TaskFactory"/> for scheduling work.
/// </summary>
public interface IHasTaskFactory
{
    public TaskFactory TaskFactory { get; }
}
