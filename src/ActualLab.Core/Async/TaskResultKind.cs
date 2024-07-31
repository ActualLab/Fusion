namespace ActualLab.Async;

public enum TaskResultKind
{
    Incomplete = 0,
    Success = 1,
    Error = 2,
    Cancellation = Error + 4,
}
