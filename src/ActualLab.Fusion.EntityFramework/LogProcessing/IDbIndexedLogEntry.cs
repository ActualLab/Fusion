namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface IDbLogEntry
{
    string Uuid { get; }
    long Version { get; set; } // Used only by events
    LogEntryState State { get; set; } // Used only by events
}

public interface IDbIndexedLogEntry : IDbLogEntry
{
    long Index { get; }
    DateTime LoggedAt { get; }
}

public interface IDbTimerLogEntry : IDbLogEntry
{
    DateTime FiresAt { get; } // Used only by timed events
}
