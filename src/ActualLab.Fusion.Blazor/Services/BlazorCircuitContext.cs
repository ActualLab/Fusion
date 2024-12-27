using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ActualLab.Internal;

namespace ActualLab.Fusion.Blazor;

public class BlazorCircuitContext(IServiceProvider services) : ProcessorBase
{
    private static long _lastId;

    private readonly TaskCompletionSource<Unit> _whenInitialized = TaskCompletionSourceExt.New<Unit>();

    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public long Id { get; } = Interlocked.Increment(ref _lastId);

    public IServiceProvider Services { get; } = services;
    [field: AllowNull, MaybeNull]
    public JSRuntimeInfo JSRuntimeInfo => field ??= Services.GetRequiredService<JSRuntimeInfo>();
    [field: AllowNull, MaybeNull]
    public Dispatcher Dispatcher => field ??= RootComponent.GetDispatcher();
    [field: AllowNull, MaybeNull]
    public NavigationManager NavigationManager => field ??= Services.GetRequiredService<NavigationManager>();

    public ComponentBase RootComponent {
        get => field ?? throw Errors.NotInitialized();
        private set;
    } = null!;

    public RenderModeDef RenderMode {
        get => field ?? throw Errors.NotInitialized();
        private set;
    } = null!;

    // ReSharper disable once InconsistentlySynchronizedField
    public Task WhenInitialized => _whenInitialized.Task;

    // Shortcuts
    public IJSRuntime? JSRuntime => JSRuntimeInfo.Runtime;
    public bool IsPrerendering => JSRuntimeInfo.IsPrerendering;
    public bool IsInteractive => JSRuntimeInfo.IsInteractive;

    public void Initialize(
        ComponentBase rootComponent,
        RenderModeDef renderMode)
    {
        lock (_whenInitialized) {
            if (_whenInitialized.Task.IsCompleted)
                throw Errors.AlreadyInitialized();

            RootComponent = rootComponent;
            RenderMode = renderMode;
            _whenInitialized.TrySetResult(default);
        }
    }
}
