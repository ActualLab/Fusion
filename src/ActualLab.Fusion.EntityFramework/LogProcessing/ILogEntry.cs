namespace ActualLab.Fusion.EntityFramework.LogProcessing;

/// <summary>
/// Defines the contract for a database log entry with UUID, version, state, and timestamp.
/// </summary>
public interface IDbLogEntry
{
    public string Uuid { get; }
    public long Version { get; set; } // Used only by events
    public LogEntryState State { get; set; } // Used only by events
    public DateTime LoggedAt { get; }
}

/// <summary>
/// Extends <see cref="IDbLogEntry"/> with a sequential index for ordered log entries
/// such as operation logs.
/// </summary>
public interface IDbIndexedLogEntry : IDbLogEntry
{
    public long Index { get; }
}

/// <summary>
/// Extends <see cref="IDbLogEntry"/> with a <c>DelayUntil</c> timestamp for timed event
/// log entries.
/// </summary>
public interface IDbEventLogEntry : IDbLogEntry
{
    public DateTime DelayUntil { get; } // Used only by timed events
}
