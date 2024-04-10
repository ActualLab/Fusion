namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface ILogEntry : IHasUuid
{
    long Index { get; } // Used only by non-timed events
    long Version { get; set; } // Used only by events
    DateTime LoggedAt { get; }
    DateTime FiresAt { get; } // Used only by timed events
    LogEntryState State { get; set; } // Used only by events
}
