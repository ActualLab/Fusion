using ActualLab.Fusion.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ActualLab.Fusion.Blazor;

public abstract class CircuitHubComponentBase : FusionComponentBase, IHasCircuitHub
{
    [Inject] protected CircuitHub CircuitHub { get; init; } = null!;

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
}
