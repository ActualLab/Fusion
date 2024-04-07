namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface ILogEntry
{
    long Index { get; }
    DateTime CommitTime { get; }
}
