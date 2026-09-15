using ActualLab.Fusion.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ActualLab.Fusion.Blazor;

/// <summary>
/// Base class for Blazor components that access <see cref="CircuitHub"/> and its
/// commonly used services (session, state factory, UICommander, etc.).
/// </summary>
public abstract class CircuitHubComponentBase : FusionComponentBase, IHasCircuitHub
{
    [Inject] protected CircuitHub CircuitHub { get; init; } = null!;
    protected Action StateHasChangedInvoker => field ??= StateHasChanged;

    // Most useful service shortcuts
    protected IServiceProvider Services => CircuitHub.Services;
    protected Session Session => CircuitHub.Session;
    protected StateFactory StateFactory => CircuitHub.StateFactory;
    protected UICommander UICommander => CircuitHub.UICommander;
    protected NavigationManager Nav => CircuitHub.Nav;
    protected IJSRuntime JS => CircuitHub.JS;

    // Explicit IHasFusionHub & IHasServices implementation
    CircuitHub IHasCircuitHub.CircuitHub => CircuitHub;
    IServiceProvider IHasServices.Services => Services;

    public void NotifyStateHasChanged(bool useSafeDispatcher = true)
    {
        try {
            var dispatcher = CircuitHub.GetDispatcher(useSafeDispatcher);
            if (dispatcher.CheckAccess())
                StateHasChanged();
            else
                _ = dispatcher.InvokeAsync(StateHasChangedInvoker);
        }
        catch (ObjectDisposedException) {
            // Intended
        }
    }
}
