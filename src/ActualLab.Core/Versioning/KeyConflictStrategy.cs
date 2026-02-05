namespace ActualLab.Versioning;

/// <summary>
/// Defines strategies for handling key conflicts during entity insertion.
/// </summary>
public enum KeyConflictStrategy
{
    Fail = 0,
    Skip,
    Update,
}
