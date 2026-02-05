namespace ActualLab.Fusion.Server;

/// <summary>
/// Extension methods for <see cref="FusionBuilder"/> to add Fusion web server services
/// for the .NET Framework (OWIN) hosting model.
/// </summary>
public static class FusionBuilderExt
{
    public static FusionWebServerBuilder AddWebServer(this FusionBuilder fusion)
        => new(fusion, null);

    public static FusionBuilder AddWebServer(this FusionBuilder fusion, Action<FusionWebServerBuilder> configure)
        => new FusionWebServerBuilder(fusion, configure).Fusion;
}
