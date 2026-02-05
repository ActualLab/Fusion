namespace ActualLab.Fusion.UI;

/// <summary>
/// Tracks running and completed <see cref="UIAction"/> instances, enabling
/// <see cref="UpdateDelayer"/> to provide instant updates during user interactions.
/// </summary>
public sealed class UIActionTracker(
    UIActionTracker.Options settings,
    IServiceProvider services
    ) : ProcessorBase, IHasServices
{
    /// <summary>
    /// Configuration options for <see cref="UIActionTracker"/>.
    /// </summary>
    public sealed record Options {
        public TimeSpan InstantUpdatePeriod { get; init; } = TimeSpan.FromMilliseconds(300);
        public MomentClock? Clock { get; init; }
    }

    private long _runningActionCount;
    private volatile AsyncState<UIAction?> _lastAction = new(null);
    private volatile AsyncState<IUIActionResult?> _lastResult = new(null);

    public Options Settings { get; } = settings;
    public IServiceProvider Services { get; } = services;
    public MomentClock Clock => field ??= Settings.Clock ?? Services.Clocks().CpuClock;
    public ILogger Log => field ??= Services.LogFor(GetType());

    public long RunningActionCount => Interlocked.Read(ref _runningActionCount);
    public AsyncState<UIAction?> LastAction => _lastAction;
    public AsyncState<IUIActionResult?> LastResult => _lastResult;

    protected override Task DisposeAsyncCore()
    {
        Interlocked.Exchange(ref _runningActionCount, 0);
        var error = new ObjectDisposedException(GetType().Name);
        _lastAction.SetFinal(error);
        _lastResult.SetFinal(error);
        return Task.CompletedTask;
    }

    public void Register(UIAction action)
    {
        lock (Lock) {
            if (StopToken.IsCancellationRequested)
                return;

            Interlocked.Increment(ref _runningActionCount);
            try {
                _lastAction = _lastAction.SetNext(action);
            }
            catch (Exception e) {
                // We need to keep this count consistent if above block somehow fails
                Interlocked.Decrement(ref _runningActionCount);
                if (e is InvalidOperationException)
                    return; // Already stopped

                Log.LogError("UI action registration failed: {Action}", action);
                throw;
            }
        }

        _ = action.WhenCompleted().ContinueWith(
            _ => {
                lock (Lock) {
                    if (StopToken.IsCancellationRequested)
                        return;

                    Interlocked.Decrement(ref _runningActionCount);

                    var result = action.UntypedResult;
                    if (result is null) {
                        Log.LogError("UI action has completed w/o a result: {Action}", action);
                        return;
                    }
                    _lastResult = _lastResult.TrySetNext(result);
                }
            },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    public bool AreInstantUpdatesEnabled()
    {
        if (RunningActionCount > 0)
            return true;

        if (LastResult.Value is not { } lastResult)
            return false;

        return lastResult.CompletedAt + Settings.InstantUpdatePeriod >= Clock.Now;
    }

    public Task WhenInstantUpdatesEnabled()
        => AreInstantUpdatesEnabled() ? Task.CompletedTask : LastAction.WhenNext();
}
