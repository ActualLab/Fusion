using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using ActualLab.Internal;

namespace ActualLab.Fusion.Blazor;

public class BlazorCircuitContext(IServiceProvider services) : ProcessorBase
{
    private static long _lastId;

    private readonly TaskCompletionSource<Unit> _whenReady = TaskCompletionSourceExt.New<Unit>();
    private volatile int _isPrerendering;

    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    public IServiceProvider Services { get; } = services;
    public long Id { get; } = Interlocked.Increment(ref _lastId);
    public Task WhenReady => _whenReady.Task;
    public bool IsPrerendering => _isPrerendering != 0;
    [field: AllowNull, MaybeNull]
    public Dispatcher Dispatcher => field ??= RootComponent.GetDispatcher();

    [field: AllowNull, MaybeNull]
    public ComponentBase RootComponent {
        get => field ?? throw Errors.NotInitialized(nameof(RootComponent));
        set {
            if (Interlocked.CompareExchange(ref field, value, null) != null)
                throw Errors.AlreadyInitialized(nameof(RootComponent));

            _whenReady.TrySetResult(default);
        }
    }

    public ClosedDisposable<(BlazorCircuitContext, int)> Prerendering(bool isPrerendering = true)
    {
        var oldIsPrerendering = Interlocked.Exchange(ref _isPrerendering, isPrerendering ? 1 : 0);
        return new ClosedDisposable<(BlazorCircuitContext Context, int OldIsPrerendering)>(
            (this, oldIsPrerendering),
            state => Interlocked.Exchange(ref state.Context._isPrerendering, state.OldIsPrerendering));
    }
}
