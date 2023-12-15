namespace ActualLab.Testing;

public class Box<T>
{
    public T Value { get; set; }

    public Box() => Value = default!;
    public Box(T value) => Value = value!;

    public override string ToString() => $"{GetType().GetName()}({Value})";
}

public static class Box
{
    public static Box<T> New<T>(T value) => new(value);
}
