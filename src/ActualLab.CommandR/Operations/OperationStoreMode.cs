namespace ActualLab.CommandR.Operations;

/// <summary>
/// What an <see cref="Operation"/>'s committed row is, and thus who reads it back.
/// A row is always written - it is also what verifies the commit on the in-doubt path.
/// </summary>
public enum OperationStoreMode
{
    /// <summary>
    /// Nobody reads it: the row exists solely to verify the commit.
    /// </summary>
    None = 0,
    /// <summary>
    /// The operation log: every host reads and processes every entry.
    /// </summary>
    Operation,
    /// <summary>
    /// The event log: exactly one host claims and processes the entry.
    /// </summary>
    Event,
}
