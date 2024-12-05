using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using ActualLab.Internal;
using Microsoft.JSInterop;

namespace ActualLab.Fusion.Blazor;

public class BlazorCircuitContext(IServiceProvider services) : ProcessorBase
{
    private static long _lastId;

    private readonly TaskCompletionSource<Unit> _whenReady = TaskCompletionSourceExt.New<Unit>();

    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public long Id { get; } = Interlocked.Increment(ref _lastId);

    public IServiceProvider Services { get; } = services;
    [field: AllowNull, MaybeNull]
    public JSRuntimeInfo JSRuntimeInfo => field ??= Services.GetRequiredService<JSRuntimeInfo>();
    public IJSRuntime JSRuntime => JSRuntimeInfo.Runtime;
    [field: AllowNull, MaybeNull]
    public Dispatcher Dispatcher => field ??= RootComponent.GetDispatcher();
    public bool IsServerSide => JSRuntimeInfo.IsRemote;
    public bool IsPrerendering => JSRuntimeInfo is { IsRemote: true, ClientProxy: null };
    public Task WhenReady => _whenReady.Task;

    [field: AllowNull, MaybeNull]
    public ComponentBase RootComponent {
        get => field ?? throw Errors.NotInitialized(nameof(RootComponent));
        set {
            if (Interlocked.CompareExchange(ref field, value, null) != null)
                throw Errors.AlreadyInitialized(nameof(RootComponent));

            _whenReady.TrySetResult(default);
        }
    }
}
