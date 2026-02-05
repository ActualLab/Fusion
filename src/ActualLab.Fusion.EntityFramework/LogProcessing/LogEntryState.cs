namespace ActualLab.Fusion.EntityFramework.LogProcessing;

/// <summary>
/// Defines processing state values for database log entries.
/// </summary>
public enum LogEntryState
{
    New = 0,
    Processed,
    Discarded,
}
