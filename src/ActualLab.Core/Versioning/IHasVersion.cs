namespace ActualLab.Versioning;

/// <summary>
/// Indicates that the implementing type exposes a version property.
/// </summary>
public interface IHasVersion<out TVersion>
    where TVersion : notnull
{
    public TVersion Version { get; }
}
