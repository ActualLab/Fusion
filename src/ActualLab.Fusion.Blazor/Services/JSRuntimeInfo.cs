using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace ActualLab.Fusion.Blazor;

// ReSharper disable once InconsistentNaming
public sealed class JSRuntimeInfo
{
    private readonly Func<object, object?> _clientProxyGetter = _ => null;

    // ReSharper disable once InconsistentNaming
    public IJSRuntime Runtime { get; init; }
    public bool IsRemote { get; init; }
    public object? ClientProxy => _clientProxyGetter.Invoke(Runtime);

    public JSRuntimeInfo(IJSRuntime runtime)
    {
        Runtime = runtime;
        var type = runtime.GetType();
        IsRemote = string.Equals(type.Name, "RemoteJSRuntime", StringComparison.Ordinal);
        if (!IsRemote)
            return;

#pragma warning disable IL2026, IL2075
        var fClientProxy = type.GetField("_clientProxy", BindingFlags.Instance | BindingFlags.NonPublic);
        if (fClientProxy != null)
            _clientProxyGetter = fClientProxy.GetGetter<object, object?>(true);
#pragma warning restore IL2026, IL2075
    }
}
