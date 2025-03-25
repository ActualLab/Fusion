namespace ActualLab.Internal;

public sealed class ErrorBox(Exception error)
{
    public readonly Exception Error = error;
}
