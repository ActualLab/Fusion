using ActualLab.Versioning;

namespace ActualLab.Fusion.Blazor;

public sealed class ByUuidAndVersionParameterComparer<TVersion> : ParameterComparer
    where TVersion : notnull
{
    public override bool AreEqual(object? oldValue, object? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
            return true; // Might be the most frequent case
        if (oldValue == null)
            return newValue == null;
        if (newValue == null)
            return false;

        var oldVersion = ((IHasVersion<TVersion>)oldValue).Version;
        var newVersion = ((IHasVersion<TVersion>)newValue).Version;
        if (!EqualityComparer<TVersion>.Default.Equals(oldVersion, newVersion))
            return false;

        var oldUuid = ((IHasUuid)oldValue).Uuid;
        var newUuid = ((IHasUuid)newValue).Uuid;
        return oldUuid == newUuid;
    }
}
