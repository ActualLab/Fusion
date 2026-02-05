namespace ActualLab.Versioning;

/// <summary>
/// Abstract base class for generating new versions from the current version.
/// </summary>
public abstract class VersionGenerator<TVersion>
{
    public abstract TVersion NextVersion(TVersion currentVersion = default!);
}
