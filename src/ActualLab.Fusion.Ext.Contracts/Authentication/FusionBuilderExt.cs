namespace ActualLab.Fusion.Authentication;

/// <summary>
/// Extension methods for <see cref="FusionBuilder"/> to register authentication client services.
/// </summary>
public static class FusionBuilderExt
{
    public static FusionBuilder AddAuthClient(this FusionBuilder fusion)
        => fusion.AddClient<IAuth>();
}
