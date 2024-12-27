using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace ActualLab.Fusion.Blazor;

// ReSharper disable once InconsistentNaming
public class JSRuntimeInfo
{
    // ReSharper disable once InconsistentNaming
    public IJSRuntime? Runtime { get; init; }
    public bool IsRemote { get; init; }
    public Func<object?> ClientProxyGetter { get; init; } = () => null;
    public object? ClientProxy => field ??= ClientProxyGetter.Invoke();
    public bool IsPrerendering => IsRemote && ClientProxy == null;
    public bool IsInteractive => Runtime != null && !IsPrerendering;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume server-side code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "We assume server-side code is fully preserved")]
    public JSRuntimeInfo(IJSRuntime? runtime)
    {
        Runtime = runtime;
        if (runtime == null)
            return;

        var type = runtime.GetType();
        if (string.Equals(type.Name, "UnsupportedJavaScriptRuntime", StringComparison.Ordinal)) {
            Runtime = null;
            return;
        }

        IsRemote = string.Equals(type.Name, "RemoteJSRuntime", StringComparison.Ordinal);
        if (!IsRemote)
            return;

        var fClientProxy = type.GetField("_clientProxy", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fClientProxy == null)
            return;

        var clientProxyGetter = fClientProxy.GetGetter<object?, object?>(true);
        ClientProxyGetter = () => clientProxyGetter.Invoke(runtime);
    }
}
