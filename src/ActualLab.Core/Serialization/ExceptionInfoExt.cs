namespace ActualLab.Serialization;

/// <summary>
/// Extension methods for converting <see cref="Exception"/> to <see cref="ExceptionInfo"/>.
/// </summary>
public static class ExceptionInfoExt
{
    public static ExceptionInfo ToExceptionInfo(this Exception? error)
        => error is null ? default : new ExceptionInfo(error);
}
