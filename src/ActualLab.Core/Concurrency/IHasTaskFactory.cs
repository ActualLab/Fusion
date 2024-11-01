namespace ActualLab.Concurrency;

public interface IHasTaskFactory
{
    TaskFactory TaskFactory { get; }
}
