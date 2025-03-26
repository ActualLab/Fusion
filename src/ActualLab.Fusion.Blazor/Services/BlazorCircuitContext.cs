using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ActualLab.Internal;

namespace ActualLab.Fusion.Blazor;

public class BlazorCircuitContext(IServiceProvider services) : ProcessorBase
{
    private static long _lastId;

    protected readonly AsyncTaskMethodBuilder WhenInitializedSource = AsyncTaskMethodBuilderExt.New();
    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public long Id { get; } = Interlocked.Increment(ref _lastId);

    public IServiceProvider Services { get; } = services;
    [field: AllowNull, MaybeNull]
    public JSRuntimeInfo JSRuntimeInfo => field ??= Services.GetRequiredService<JSRuntimeInfo>();
    [field: AllowNull, MaybeNull]
    public NavigationManager NavigationManager => field ??= Services.GetRequiredService<NavigationManager>();

    public Dispatcher Dispatcher {
        get => field ?? throw Errors.NotInitialized();
        protected set;
    } = null!;

    public RenderModeDef RenderMode {
        get => field ?? throw Errors.NotInitialized();
        protected set;
    } = null!;

    // ReSharper disable once InconsistentlySynchronizedField
    public Task WhenInitialized => WhenInitializedSource.Task;

    // Shortcuts
    public IJSRuntime? JSRuntime => JSRuntimeInfo.Runtime;
    public bool IsPrerendering => JSRuntimeInfo.IsPrerendering;
    public bool IsInteractive => JSRuntimeInfo.IsInteractive;

    public virtual void Initialize(
        Dispatcher dispatcher,
        RenderModeDef renderMode)
    {
        lock (Lock) {
            if (IsDisposed)
                throw Errors.AlreadyDisposed();

            if (WhenInitializedSource.Task.IsCompleted) {
                if (Dispatcher == dispatcher && RenderMode == renderMode)
                    return;

                throw Errors.AlreadyInitialized();
            }

            Dispatcher = dispatcher;
            RenderMode = renderMode;
            WhenInitializedSource.TrySetResult();
        }
    }
}
