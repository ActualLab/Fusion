using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Fusion.Authentication;

namespace ActualLab.Fusion.Blazor.Authentication;

public static class FusionBlazorBuilderExt
{
    public static FusionBlazorBuilder AddAuthentication(
        this FusionBlazorBuilder fusionBlazor,
        Action<AuthorizationOptions>? configure = null)
    {
        var services = fusionBlazor.Services;
        if (services.HasService<ClientAuthHelper>())
            return fusionBlazor;

        services.AddAuthorizationCore(configure ?? (_ => {})); // .NET 5.0 doesn't allow to pass null here
        services.RemoveAll(typeof(AuthenticationStateProvider));
        services.AddSingleton(_ => AuthStateProvider.Options.Default);
        services.AddScoped<AuthenticationStateProvider>(c => new AuthStateProvider(
            c.GetRequiredService<AuthStateProvider.Options>(), c));
        services.AddScoped(c => (AuthStateProvider)c.GetRequiredService<AuthenticationStateProvider>());
        services.AddScoped(c => new ClientAuthHelper(c));
        return fusionBlazor;
    }

    public static FusionBlazorBuilder AddPresenceReporter(
        this FusionBlazorBuilder fusionBlazor,
        Func<IServiceProvider, PresenceReporter.Options>? optionsFactory = null)
    {
        var services = fusionBlazor.Services;
        services.AddSingleton(optionsFactory, _ => PresenceReporter.Options.Default);
        if (services.HasService<PresenceReporter>())
            return fusionBlazor;

        services.AddScoped(c => new PresenceReporter(c.GetRequiredService<PresenceReporter.Options>(), c));
        return fusionBlazor;
    }
}
