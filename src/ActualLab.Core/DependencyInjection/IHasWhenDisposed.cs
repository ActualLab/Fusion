namespace ActualLab.DependencyInjection;

/// <summary>
/// Indicates a type that exposes its disposal status.
/// </summary>
public interface IHasDisposeStatus
{
    public bool IsDisposed { get; }
}

/// <summary>
/// Extends <see cref="IHasDisposeStatus"/> with a task that completes upon disposal.
/// </summary>
public interface IHasWhenDisposed : IHasDisposeStatus
{
    public Task? WhenDisposed { get; }
}
