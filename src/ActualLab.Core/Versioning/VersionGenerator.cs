namespace ActualLab.Versioning;

public abstract class VersionGenerator<TVersion>
{
    public abstract TVersion NextVersion(TVersion currentVersion = default!);
}
