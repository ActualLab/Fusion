using System.Web;
using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

public class RenderModeHelper(CircuitHub circuitHub)
{
    protected CircuitHub CircuitHub { get; } = circuitHub;

    public RenderModeDef? CurrentMode => CircuitHub.WhenInitialized.IsCompleted
        ? CircuitHub.RenderMode
        : null;

    public virtual string GetCurrentModeTitle()
    {
        var currentMode = CurrentMode;
        if (currentMode is null)
            return "Loading...";

        var actualMode = string.Equals(currentMode.Key, "a", StringComparison.Ordinal)
            ? OSInfo.IsWebAssembly
                ? RenderModeDef.ByKey.GetValueOrDefault("w")
                : RenderModeDef.ByKey.GetValueOrDefault("s")
            : null;
        return actualMode is null
            ? currentMode.Title
            : $"{currentMode.Title} ({actualMode.Title})";
    }

    public virtual void ChangeMode(RenderModeDef renderMode)
    {
        if (CurrentMode == renderMode)
            return;

        var navigationManager = CircuitHub.Nav;
        var switchUrl = GetModeChangeUrl(renderMode.Key, navigationManager.Uri);
        navigationManager.NavigateTo(switchUrl, true);
    }

    public virtual string GetModeChangeUrl(string renderModeKey, string? redirectTo = null)
    {
        redirectTo ??= "/";
        return $"/fusion/renderMode/{HttpUtility.UrlEncode(renderModeKey)}?redirectTo={HttpUtility.UrlEncode(redirectTo)}";
    }
}
