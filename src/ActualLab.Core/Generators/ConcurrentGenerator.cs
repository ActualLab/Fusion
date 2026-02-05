namespace ActualLab.Generators;

/// <summary>
/// A thread-safe <see cref="Generator{T}"/> that uses striped generation
/// to reduce contention in concurrent scenarios.
/// </summary>
public abstract class ConcurrentGenerator<T> : Generator<T>
{
    public abstract T Next(int random);
    public override T Next() => Next(Environment.CurrentManagedThreadId);
}
