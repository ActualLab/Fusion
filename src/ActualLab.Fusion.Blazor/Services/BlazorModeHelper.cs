using System.Web;
using Microsoft.AspNetCore.Components;

namespace ActualLab.Fusion.Blazor;

public class BlazorModeHelper(NavigationManager navigator, JSRuntimeInfo jsRuntimeInfo)
{
    public bool IsBlazorServer = jsRuntimeInfo.IsRemote;

    public virtual void ChangeMode(bool isBlazorServer)
    {
        if (IsBlazorServer == isBlazorServer)
            return;

        var switchUrl = GetModeChangeUrl(isBlazorServer, navigator.Uri);
        navigator.NavigateTo(switchUrl, true);
    }

    public virtual string GetModeChangeUrl(bool isBlazorServer, string? redirectTo = null)
    {
        redirectTo ??= "/";
        return $"/fusion/blazorMode/{(isBlazorServer ? "1" : "0")}?redirectTo={HttpUtility.UrlEncode(redirectTo)}";
    }
}
