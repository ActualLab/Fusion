using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ActualLab.Fusion.Blazor;

public sealed record RenderModeDef(
    string Key,
    string Title)
{
#if NET8_0_OR_GREATER
    public required IComponentRenderMode Mode { get; init; }
#else
    public bool IsWebAssembly { get; init; }
    public bool Prerender { get; init; }
#endif

    public static RenderModeDef[] All {
        get;
        set {
            field = value;
            ByKey = All.ToDictionary(x => x.Key, x => x, StringComparer.Ordinal);
        }
    }
    public static IReadOnlyDictionary<string, RenderModeDef> ByKey { get; private set; }
    public static RenderModeDef Default { get; set; }

    static RenderModeDef()
    {
        ByKey = null!;
        All = [
#if NET8_0_OR_GREATER
            new("a", "Auto") { Mode = new InteractiveAutoRenderMode(prerender: true) },
            new("s", "Server") { Mode = new InteractiveServerRenderMode(prerender: true) },
            new("w", "WASM") { Mode = new InteractiveWebAssemblyRenderMode(prerender: true) },
#else
            new("s", "Server") { IsWebAssembly = false, Prerender = true},
            new("w", "WASM") { IsWebAssembly = true, Prerender = true },
#endif
        ];
        Default = All[0];
    }

    public static RenderModeDef GetOrDefault(string? key)
        => ByKey.GetValueOrDefault(key ?? "", Default)!;
}
