namespace ActualLab.Fusion.EntityFramework.LogProcessing;

public interface ILogEntry : IHasUuid
{
    long Index { get; }
    long Version { get; set; }
    DateTime LoggedAt { get; }
    bool IsProcessed { get; set; }
}
