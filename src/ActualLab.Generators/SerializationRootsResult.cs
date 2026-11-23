namespace ActualLab.Generators;

internal sealed record SerializationRootsResult(string? Code, ImmutableArray<Diagnostic> Diagnostics)
{
    public static readonly SerializationRootsResult None = new(null, ImmutableArray<Diagnostic>.Empty);
}
