namespace ActualLab.Async;

/// <summary>
/// Defines the possible result states of a task.
/// </summary>
public enum TaskResultKind
{
    Incomplete = 0,
    Success = 1,
    Error = 2,
    Cancellation = Error + 4,
}
