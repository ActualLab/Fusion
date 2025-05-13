using ActualLab.Fusion.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ActualLab.Fusion.Blazor;

public class FusionComponentBase : SmartComponentBase, IHasFusionHub
{
    [Inject] protected FusionHub FusionHub { get; init; } = null!;

    // Most useful service shortcuts
    protected IServiceProvider Services => FusionHub.Services;
    protected Session Session => FusionHub.Session;
    protected StateFactory StateFactory => FusionHub.StateFactory;
    protected UICommander UICommander => FusionHub.UICommander;
    protected NavigationManager Nav => FusionHub.Nav;
    protected IJSRuntime JS => FusionHub.JS;

    // Explicit IHasFusionHub & IHasServices implementation
    FusionHub IHasFusionHub.FusionHub => FusionHub;
    IServiceProvider IHasServices.Services => Services;
}
