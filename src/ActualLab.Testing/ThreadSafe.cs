namespace ActualLab.Testing;

/// <summary>
/// A thread-safe wrapper around a value of type <typeparamref name="T"/>
/// that synchronizes all reads and writes with a lock.
/// </summary>
public class ThreadSafe<T>(T value)
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    public T Value {
        get {
            lock (_lock)
                return field;
        }
        set {
            lock (_lock)
                field = value;
        }
    } = value!;

    public ThreadSafe() : this(default!) { }

    public override string ToString() => $"{GetType().GetName()}({Value})";

    public static implicit operator ThreadSafe<T>(T value) => new(value);
    public static implicit operator T(ThreadSafe<T> value) => value.Value;
}

/// <summary>
/// Factory methods for creating <see cref="ThreadSafe{T}"/> instances.
/// </summary>
public static class ThreadSafe
{
    public static ThreadSafe<T> New<T>(T value) => new(value);
}
