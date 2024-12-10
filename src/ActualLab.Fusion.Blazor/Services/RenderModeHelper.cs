using System.Web;
using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

public class RenderModeHelper(BlazorCircuitContext circuitContext)
{
    protected BlazorCircuitContext CircuitContext { get; } = circuitContext;

    public RenderModeDef? CurrentMode => CircuitContext.WhenInitialized.IsCompleted
        ? CircuitContext.RenderMode
        : null;

    public virtual string GetCurrentModeTitle()
    {
        var currentMode = CurrentMode;
        if (currentMode == null)
            return "Loading...";

        var actualMode = string.Equals(currentMode.Key, "a")
            ? OSInfo.IsWebAssembly
                ? RenderModeDef.ByKey.GetValueOrDefault("w")
                : RenderModeDef.ByKey.GetValueOrDefault("s")
            : null;
        return actualMode == null
            ? currentMode.Title
            : $"{currentMode.Title} ({actualMode.Title})";
    }

    public virtual void ChangeMode(RenderModeDef renderMode)
    {
        if (CurrentMode == renderMode)
            return;

        var navigationManager = CircuitContext.NavigationManager;
        var switchUrl = GetModeChangeUrl(renderMode.Key, navigationManager.Uri);
        navigationManager.NavigateTo(switchUrl, true);
    }

    public virtual string GetModeChangeUrl(string renderModeKey, string? redirectTo = null)
    {
        redirectTo ??= "/";
        return $"/fusion/blazorMode/{HttpUtility.UrlEncode(renderModeKey)}?redirectTo={HttpUtility.UrlEncode(redirectTo)}";
    }
}
