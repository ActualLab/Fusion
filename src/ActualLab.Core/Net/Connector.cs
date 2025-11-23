using ActualLab.Internal;
using ActualLab.Resilience;

namespace ActualLab.Net;

public sealed class Connector<TConnection> : WorkerBase
    where TConnection : class
{
    private readonly Func<CancellationToken, Task<TConnection>> _connectionFactory;
    private volatile AsyncState<State> _state = new(State.New());
    private long _reconnectsAt;
    private bool _resetTryIndex;

    public AsyncState<Result<bool>> IsConnected { get; private set; } = new(false);
    public Moment? ReconnectsAt { // Relative to CpuClock.Now
        get {
            var reconnectsAt = Interlocked.Read(ref _reconnectsAt);
            return reconnectsAt == default ? null : new Moment(reconnectsAt);
        }
    }

    public Func<TConnection, CancellationToken, Task>? Connected { get; init; }
    public TransiencyResolver TransiencyResolver { get; init; } = TransiencyResolvers.PreferTransient;
    public ExceptionFilter ReconnectOn { get; init; } = ExceptionFilters.AnyNonTerminal;
    public IRetryDelayer ReconnectDelayer { get; init; } = new RetryDelayer();
    public ILogger? Log { get; init; }
    public LogLevel LogLevel { get; init; } = LogLevel.Debug;
    public string LogTag { get; init; }

    public Connector(Func<CancellationToken, Task<TConnection>> connectionFactory)
    {
        _connectionFactory = connectionFactory;
        LogTag = GetType().GetName();
    }

    public Task<TConnection> GetConnection(CancellationToken cancellationToken = default)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        var state = _state;
        var stateValue = state.Value;
        return stateValue.ConnectionTask.IsCompletedSuccessfully()
            ? stateValue.ConnectionTask
            : AwaitConnection();

        async Task<TConnection> AwaitConnection()
        {
            this.Start();
            while (true) {
                try {
                    return await state.Value.ConnectionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                    state = await state.WhenNext(cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    public void DropConnection(TConnection connection, Exception? error)
    {
        AsyncState<State> prevState;
        lock (Lock) {
            prevState = _state;
            if (!prevState.Value.ConnectionTask.IsCompleted)
                return; // Nothing to do: not yet connected
#pragma warning disable VSTHRD104
            if (connection != prevState.Value.ConnectionTask.GetAwaiter().GetResult())
                return; // The connection is already renewed
#pragma warning restore VSTHRD104

            _state = prevState.SetNext(State.New() with {
                LastError = error,
            });
        }
        prevState.Value.Dispose();
    }

    public void ResetTryIndex()
    {
        lock (Lock)
            _resetTryIndex = true;
    }

    // Protected & private methods

    protected override async Task OnRun(CancellationToken cancellationToken)
    {
        AsyncState<State>? state;
        lock (Lock)
            state = _state;
        while (true) {
            var connectionSource = state.Value.ConnectionSource;
            var connectionTask = connectionSource.Task;
            TConnection? connection = null;
            Exception? error = null;
            try {
                Log.IfEnabled(LogLevel)?.Log(LogLevel, "{LogTag}: Connecting...", LogTag);
                if (!connectionTask.IsCompleted)
                    connection = await _connectionFactory.Invoke(cancellationToken).ConfigureAwait(false);
                else // Something
                    connection = await connectionTask.ConfigureAwait(false);
                connectionSource.TrySetResult(connection);
            }
            catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                error = e;
                connectionSource.TrySetException(e);
            }

            if (connection is not null) {
                lock (Lock) {
                    state = _state = _state.SetNext(new State(connectionSource));
                    IsConnected = IsConnected.SetNext(true);
                }

                Log.IfEnabled(LogLevel)?.Log(LogLevel, "{LogTag}: Connected", LogTag);
                try {
                    if (Connected is not null)
                        await Connected.Invoke(connection, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (!e.IsCancellationOf(cancellationToken)) {
                    Log?.LogWarning(e, "{LogTag}: Connected handler failed", LogTag);
                }
                await state.WhenNext(cancellationToken).ConfigureAwait(false);
            }

            lock (Lock) {
                if (state == _state) {
                    var oldState = state;
                    var newTryIndex = state.Value.TryIndex + 1;
                    if (_resetTryIndex) {
                        _resetTryIndex = false;
                        newTryIndex = 0;
                    }
                    state = _state = oldState.SetNext(State.New() with {
                        LastError = error,
                        TryIndex = newTryIndex,
                    });
                    oldState.Value.Dispose();
                }
                else {
                    // It was updated by Reconnect, so we just switch to the new state
                    state = _state;
                    error = state.Value.LastError;
                }

                if (error is not null) {
                    IsConnected = IsConnected.SetNext(Result.NewError<bool>(error));
                    Log?.LogError(error, "{LogTag}: Disconnected", LogTag);
                }
                else {
                    IsConnected = IsConnected.SetNext(false);
                    Log?.LogError("{LogTag}: Disconnected", LogTag);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (error is not null && !ReconnectOn.Invoke(error, TransiencyResolver))
                throw error;

            if (state.Value.TryIndex is var tryIndex and > 0) {
                var delayLogger = new RetryDelayLogger("reconnect", LogTag, Log, LogLevel);
                var delay = ReconnectDelayer.GetDelay(tryIndex, delayLogger, cancellationToken);
                if (delay.IsLimitExceeded)
                    throw new RetryLimitExceededException();

                if (!delay.Task.IsCompleted) {
                    Interlocked.Exchange(ref _reconnectsAt, delay.EndsAt.EpochOffsetTicks);
                    try {
                        await delay.Task.ConfigureAwait(false);
                    }
                    finally {
                        Interlocked.Exchange(ref _reconnectsAt, 0);
                    }
                }
            }
        }
    }

    protected override Task OnStop()
    {
        lock (Lock) {
            var prevState = _state;
            if (!prevState.Value.ConnectionTask.IsCompleted)
                prevState.Value.ConnectionSource.TrySetCanceled(StopToken);

            _state = prevState.SetNext(State.NewCancelled(StopToken));
            _state.SetFinal(StopToken); // StopToken is cancelled here
            prevState.Value.Dispose();

            var (isConnected, error) = IsConnected.Value;
            if (error is not null && isConnected)
                IsConnected = IsConnected.SetNext(false);
            IsConnected.SetFinal(Errors.AlreadyDisposed(GetType()));
        }
        return Task.CompletedTask;
    }

    // Nested types

    [StructLayout(LayoutKind.Auto)]
    private sealed record State(
        AsyncTaskMethodBuilder<TConnection> ConnectionSource,
        Exception? LastError = null,
        int TryIndex = 0) : IDisposable
    {
        public Task<TConnection> ConnectionTask => ConnectionSource.Task;

        public static State New()
            => new(AsyncTaskMethodBuilderExt.New<TConnection>());
        public static State NewCancelled(CancellationToken cancellationToken)
            => new(AsyncTaskMethodBuilderExt.New<TConnection>().WithCancellation(cancellationToken));

        public void Dispose()
        {
            var connectionTask = ConnectionTask;
            if (!ConnectionTask.IsCompletedSuccessfully())
                return;

            // Dispose the connection
            _ = Task.Run(async () => {
                var connection = await connectionTask.ConfigureAwait(false);
                if (connection is IAsyncDisposable ad)
                    _ = ad.DisposeAsync();
                else if (connection is IDisposable d)
                    d.Dispose();
            }, CancellationToken.None);
        }
    }
}
