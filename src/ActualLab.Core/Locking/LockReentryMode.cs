namespace ActualLab.Locking;

/// <summary>
/// Defines lock reentry behavior modes for async locks.
/// </summary>
public enum LockReentryMode
{
    Unchecked = 0,
    CheckedFail,
    CheckedPass,
}
