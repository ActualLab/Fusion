namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbLogEntry
{
    string Uuid { get; }
    long Version { get; set; } // Used only by events
    LogEntryState State { get; set; } // Used only by events
    DateTime LoggedAt { get; }
}

public interface IDbIndexedLogEntry : IDbLogEntry
{
    long Index { get; }
}

public interface IDbEventLogEntry : IDbLogEntry
{
    DateTime DelayUntil { get; } // Used only by timed events
}
