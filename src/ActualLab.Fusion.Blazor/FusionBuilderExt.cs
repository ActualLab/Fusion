namespace ActualLab.Fusion.Blazor;

/// <summary>
/// Extension methods for <see cref="FusionBuilder"/> to add Blazor integration.
/// </summary>
public static class FusionBuilderExt
{
    public static FusionBlazorBuilder AddBlazor(this FusionBuilder fusion)
        => new(fusion, null);

    public static FusionBuilder AddBlazor(this FusionBuilder fusion,
        Action<FusionBlazorBuilder>? configure)
        => new FusionBlazorBuilder(fusion, configure).Fusion;
}
