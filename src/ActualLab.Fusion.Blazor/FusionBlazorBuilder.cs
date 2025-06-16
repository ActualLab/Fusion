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

        // We want the above Contains call to run in O(1), so...
        services.Insert(0, AddedTagDescriptor);
        services.AddScoped(c => new UICommander(c));
        services.AddScoped(_ => new UIActionFailureTracker.Options());
        services.AddScoped(c => new UIActionFailureTracker(
            c.GetRequiredService<UIActionFailureTracker.Options>(), c));
        services.AddScopedOrSingleton((c, _) => {
            try {
                // Note that IJSRuntime is registered as a singleton (i.e., it's non-scoped) in Blazor WASM
                return new JSRuntimeInfo(c.GetService<IJSRuntime>());
            }
            catch {
                // No IJSRuntime / the container is getting disposed / etc.
                return new JSRuntimeInfo(null);
            }
        });
        services.AddScoped(c => new RenderModeHelper(c.GetRequiredService<CircuitHub>()));
        services.AddScoped(c => new CircuitHub(c));

        configure?.Invoke(this);
    }
}
