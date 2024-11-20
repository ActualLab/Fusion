namespace ActualLab.Concurrency;

public interface IHasTaskFactory
{
    public TaskFactory TaskFactory { get; }
}
