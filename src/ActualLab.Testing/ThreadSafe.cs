namespace ActualLab.Testing;

public class ThreadSafe<T>(T value)
{
    private readonly Lock _lock = LockFactory.Create();
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

public static class ThreadSafe
{
    public static ThreadSafe<T> New<T>(T value) => new(value);
}
