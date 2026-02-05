namespace ActualLab.Internal;

/// <summary>
/// A simple wrapper that boxes an <see cref="Exception"/> as a reference-typed container.
/// </summary>
public sealed class ErrorBox(Exception error)
{
    public readonly Exception Error = error;
}
