using ActualLab.Versioning;

namespace ActualLab.Fusion.Blazor;

/// <summary>
/// A <see cref="ParameterComparer"/> that compares parameters by their <see cref="IHasVersion{TVersion}.Version"/>.
/// </summary>
public sealed class ByVersionParameterComparer<TVersion> : ParameterComparer
    where TVersion : notnull
{
    public static ByVersionParameterComparer<TVersion> Instance { get; } = new();

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
        return EqualityComparer<TVersion>.Default.Equals(oldVersion, newVersion);
    }
}
