namespace ActualLab.DependencyInjection;

public interface IHasDisposeStatus
{
    public bool IsDisposed { get; }
}

public interface IHasWhenDisposed : IHasDisposeStatus
{
    public Task? WhenDisposed { get; }
}
