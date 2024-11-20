namespace ActualLab.DependencyInjection;

public interface IHasIsDisposed
{
    public bool IsDisposed { get; }
}

public interface IHasWhenDisposed : IHasIsDisposed
{
    public Task? WhenDisposed { get; }
}
