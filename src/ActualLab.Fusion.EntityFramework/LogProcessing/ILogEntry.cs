namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbLogEntry
{
    public string Uuid { get; }
    public long Version { get; set; } // Used only by events
    public LogEntryState State { get; set; } // Used only by events
    public DateTime LoggedAt { get; }
}

public interface IDbIndexedLogEntry : IDbLogEntry
{
    public long Index { get; }
}

public interface IDbEventLogEntry : IDbLogEntry
{
    public DateTime DelayUntil { get; } // Used only by timed events
}
