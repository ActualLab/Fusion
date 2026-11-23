#if NET8_0_OR_GREATER // Source-generated System.Text.Json metadata matters for NativeAOT only
namespace ActualLab.Fusion.Extensions.Internal;

#pragma warning disable CA2255

/// <summary>
/// Module initializer that adds the System.Text.Json metadata of this assembly's types to
/// <see cref="SystemJsonSerializer.TypeInfoResolver"/>.
/// </summary>
internal static class FusionExtBackendContractsModuleInitializer
{
#if NET8_0_OR_GREATER
    [ModuleInitializer]
#endif
    internal static void Initialize()
        => SystemJsonSerializer.TypeInfoResolver.Add(FusionExtBackendJsonContext.Default);
}
#endif
