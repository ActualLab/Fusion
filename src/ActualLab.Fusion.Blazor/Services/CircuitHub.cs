using System.Diagnostics.CodeAnalysis;
using ActualLab.Fusion.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ActualLab.Internal;

namespace ActualLab.Fusion.Blazor;

/// <summary>
/// <see cref="CircuitHub"/> is a scoped service caching a set of most frequently used Blazor & Fusion services.
/// In addition to that, it enables access to Blazor <see cref="Dispatcher"/>
/// and provides information about the current <see cref="RenderMode"/>.
/// </summary>
public class CircuitHub : ProcessorBase, IHasServices
{
    private static long _lastId;

    protected readonly AsyncTaskMethodBuilder WhenInitializedSource = AsyncTaskMethodBuilderExt.New();

    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public long Id { get; } = Interlocked.Increment(ref _lastId);

    public IServiceProvider Services { get; }
    public StateFactory StateFactory { get; }
    public UICommander UICommander { get; }
    public ICommander Commander { get; }
    // Some services require lazy resolution
    [field: AllowNull, MaybeNull]
    public Session Session => field ??= Services.GetRequiredService<Session>();
    [field: AllowNull, MaybeNull]
    public ISessionResolver SessionResolver => field ??= Services.GetRequiredService<ISessionResolver>();
    [field: AllowNull, MaybeNull]
    public NavigationManager Nav => field ??= Services.GetRequiredService<NavigationManager>();
    [field: AllowNull, MaybeNull]
    public JSRuntimeInfo JSRuntimeInfo => field ??= Services.GetRequiredService<JSRuntimeInfo>();
    [field: AllowNull, MaybeNull]
    public IJSRuntime JS => field ??= Services.GetRequiredService<IJSRuntime>();
    // Useful shortcuts
    public bool IsPrerendering => JSRuntimeInfo.IsPrerendering;
    public bool IsInteractive => JSRuntimeInfo.IsInteractive;

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

    public CircuitHub(IServiceProvider services)
    {
        Services = services;
        StateFactory = services.StateFactory();
        UICommander = services.UICommander();
        Commander = UICommander.Commander;
    }

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
