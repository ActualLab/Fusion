namespace ActualLab.Generators;

/// <summary>
/// Abstract base class for value generators that produce a sequence of values.
/// </summary>
public abstract class Generator<T>
{
    public abstract T Next();
}
