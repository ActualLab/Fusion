namespace ActualLab.Interception;

public abstract class ArgumentListWriter
{
    public abstract T OnStruct<T>(int index);
    public abstract object? OnClass(Type type, int index);
    public abstract object? OnAny(Type type, int index, object? defaultValue);
}
