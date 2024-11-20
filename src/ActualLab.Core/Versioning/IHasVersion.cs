namespace ActualLab.Versioning;

public interface IHasVersion<out TVersion>
    where TVersion : notnull
{
    public TVersion Version { get; }
}
