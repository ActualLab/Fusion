using ActualLab.Versioning;

namespace ActualLab.Fusion.Blazor;

public sealed class ByUuidAndVersionParameterComparer<TVersion> : ParameterComparer
    where TVersion : notnull
{
    public static ByUuidAndVersionParameterComparer<TVersion> Instance { get; } = new();

    public override bool AreEqual(object? oldValue, object? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
            return true; // Might be the most frequent case
        if (oldValue is null)
            return newValue is null;
        if (newValue is null)
            return false;

        var oldVersion = ((IHasVersion<TVersion>)oldValue).Version;
        var newVersion = ((IHasVersion<TVersion>)newValue).Version;
        if (!EqualityComparer<TVersion>.Default.Equals(oldVersion, newVersion))
            return false;

        var oldUuid = ((IHasUuid)oldValue).Uuid;
        var newUuid = ((IHasUuid)newValue).Uuid;
        return string.Equals(oldUuid, newUuid, StringComparison.Ordinal);
    }
}
