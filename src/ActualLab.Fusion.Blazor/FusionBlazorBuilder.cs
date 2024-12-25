using ActualLab.Fusion.UI;
using Microsoft.JSInterop;

namespace ActualLab.Fusion.Blazor;

public readonly struct FusionBlazorBuilder
{
    private sealed class AddedTag;
    private static readonly ServiceDescriptor AddedTagDescriptor = new(typeof(AddedTag), new AddedTag());

    public FusionBuilder Fusion { get; }
    public IServiceCollection Services => Fusion.Services;

    internal FusionBlazorBuilder(
        FusionBuilder fusion,
        Action<FusionBlazorBuilder>? configure)
    {
        Fusion = fusion;
        var services = Services;
        if (services.Contains(AddedTagDescriptor)) {
            configure?.Invoke(this);
            return;
        }

        // We want above Contains call to run in O(1), so...
        services.Insert(0, AddedTagDescriptor);
        services.AddScoped(c => new UICommander(c));
        services.AddScoped(_ => new UIActionFailureTracker.Options());
        services.AddScoped(c => new UIActionFailureTracker(
            c.GetRequiredService<UIActionFailureTracker.Options>(), c));
        services.AddScopedOrSingleton(c => new JSRuntimeInfo(c.GetService<IJSRuntime>()));
        services.AddScoped(c => new RenderModeHelper(c.GetRequiredService<BlazorCircuitContext>()));
        services.AddScoped(c => new BlazorCircuitContext(c));
    }
}
