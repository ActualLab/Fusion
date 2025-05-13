using System.Web;
using ActualLab.OS;

namespace ActualLab.Fusion.Blazor;

public class RenderModeHelper(UIHub uiHub)
{
    protected UIHub UIHub { get; } = uiHub;

    public RenderModeDef? CurrentMode => UIHub.WhenInitialized.IsCompleted
        ? UIHub.RenderMode
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

        var navigationManager = UIHub.Nav;
        var switchUrl = GetModeChangeUrl(renderMode.Key, navigationManager.Uri);
        navigationManager.NavigateTo(switchUrl, true);
    }

    public virtual string GetModeChangeUrl(string renderModeKey, string? redirectTo = null)
    {
        redirectTo ??= "/";
        return $"/fusion/renderMode/{HttpUtility.UrlEncode(renderModeKey)}?redirectTo={HttpUtility.UrlEncode(redirectTo)}";
    }
}
