using ActualLab.Fusion.Server.Internal;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace ActualLab.Fusion.Server;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register Fusion server services.
/// </summary>
public static class ServiceCollectionExt
{
    public static IServiceCollection AddBlazorCircuitActivitySuppressor(this IServiceCollection services)
        => services.AddScoped<CircuitHandler, BlazorCircuitActivitySuppressor>();
}
