namespace ActualLab.Interception;

/// <summary>
/// Abstract visitor for reading items from an <see cref="ArgumentList"/>.
/// </summary>
public abstract class ArgumentListReader
{
    public abstract void OnStruct<T>(T item, int index);
    public abstract void OnClass(Type type, object? item, int index);
    public abstract void OnAny(Type type, object? item, int index);
}
