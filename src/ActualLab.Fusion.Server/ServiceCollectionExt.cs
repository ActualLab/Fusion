using ActualLab.Fusion.Server.Internal;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace ActualLab.Fusion.Server;

public static class ServiceCollectionExt
{
    public static IServiceCollection AddBlazorCircuitActivitySuppressor(this IServiceCollection services)
        => services.AddScoped<CircuitHandler, BlazorCircuitActivitySuppressor>();
}
