using ActualLab.OS;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Base class for tracking open RPC calls (inbound or outbound) on a peer.
/// </summary>
public abstract class RpcCallTracker<TRpcCall> : IEnumerable<TRpcCall>
    where TRpcCall : RpcCall
{
    protected RpcLimits Limits { get; private set; } = null!;
    protected readonly ConcurrentDictionary<long, TRpcCall> Calls = new(HardwareInfo.ProcessorCountPo2, 131);

    [field: AllowNull, MaybeNull]
    public RpcPeer Peer {
        get;
        protected set {
            if (field is not null)
                throw Errors.AlreadyInitialized(nameof(Peer));

            field = value;
            Limits = field.Hub.Limits;
        }
    }

    public int Count => Calls.Count;

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    // ReSharper disable once NotDisposedResourceIsReturned
    public IEnumerator<TRpcCall> GetEnumerator() => Calls.Values.GetEnumerator();

    public virtual void Initialize(RpcPeer peer)
        => Peer = peer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TRpcCall? Get(long callId)
        => Calls.GetValueOrDefault(callId);
}

/// <summary>
/// Tracks active inbound RPC calls on a peer.
/// </summary>
public sealed class RpcInboundCallTracker : RpcCallTracker<RpcInboundCall>
{
    public RpcInboundCall this[long id] => Calls[id];

    internal RpcCallStageCounts GetStageCounts()
    {
        var result = default(RpcCallStageCounts);
        foreach (var call in this)
            result.Add(call.CompletedStage);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RpcInboundCall GetOrRegister(RpcInboundCall call)
    {
        if (call.NoWait || Calls.TryAdd(call.Id, call))
            return call;

        // We could use this call earlier, but it's more expensive,
        // and we should rarely land here, so we do this separately
        return Calls.GetOrAdd(call.Id, static (_, call1) => call1, call);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Unregister(RpcInboundCall call)
        // NoWait should always return true here!
        => call.NoWait || Calls.TryRemove(call.Id, call);

    public void Clear()
        => Calls.Clear();
}

/// <summary>
/// Tracks active outbound RPC calls on a peer, handling timeouts, reconnection, and abort.
/// </summary>
public sealed class RpcOutboundCallTracker : RpcCallTracker<RpcOutboundCall>
{
    private readonly ConcurrentDictionary<long, RpcOutboundCall> _longLivingCalls = new(HardwareInfo.ProcessorCountPo2, 131);
    private RpcCallStageCounts _reportedInboundCallCounts;
    private RpcCallStageCounts _reportedOutboundCallCounts;
    private NewCallTracker? _newCallTracker;
    private CpuTimestamp _lastCallMetricsRefreshAt;
    private long _lastId;

    public RpcOutboundCall this[long id] => Calls[id];

    public void Register(RpcOutboundCall call)
    {
        if (call.NoWait)
            throw new ArgumentOutOfRangeException(nameof(call), "call.NoWait == true.");
        if (call.Id != 0)
            throw new ArgumentOutOfRangeException(nameof(call), "call.Id != 0.");

        call.Id = Interlocked.Increment(ref _lastId);
        call.StartedAt = CpuTimestamp.Now;
        Calls.TryAdd(call.Id, call); // Must succeed for unique call.Id
        if (call.IsLongLiving)
            _longLivingCalls.TryAdd(call.Id, call);  // Must succeed for unique call.Id
        Volatile.Read(ref _newCallTracker)?.Notify();
    }

    // Called once the connected state is published, so a call registering concurrently either
    // is seen here or sees that state itself and sends from RpcOutboundCall.Invoke.
    public void SendUnsent()
    {
        foreach (var call in Calls.Values)
            if (call is { IsSent: false, ResultTask.IsCompleted: false })
                call.TrySendRegistered();
    }

    public List<RpcOutboundCall> GetSentCalls()
    {
        // Callers must snapshot before the new connection state is exposed - that releases
        // every call parked on WhenConnected, making queued and in-flight ones alike.
        var calls = new List<RpcOutboundCall>();
        foreach (var call in Calls.Values)
            if (call.IsSent)
                calls.Add(call);
        return calls;
    }

    public void AbortOwnHubCalls(RpcHandshake handshake)
    {
        if (handshake.RemoteHubId != Peer.Hub.Id)
            return; // Not own hub

        foreach (var call in Calls.Values) {
            if (call.ResultTask.IsCompleted)
                continue; // Too late to abort
            if (!call.Context.MustNotCallOwnHub)
                continue; // Fine to run this call on our own hub
            if (call.GetOwnHubCallError(handshake) is not { } error)
                continue; // No error provided

            call.SetError(error, context: null, assumeCancelled: false);
        }
    }

    // Started by RpcPeer.OnRun once per outage. It owns both deadlines a registered call has while
    // the peer is away and only delivers them: what each one means is up to the call
    // (see RpcOutboundCall.On* handlers). Unsent calls are in scope too, so RpcOutboundCall never
    // times itself out and a connected peer runs nothing at all.
    //
    // Both deadlines run from the later of the disconnect and the call, so a call issued mid-outage
    // still gets its full timeout.
    //
    // Calls.Count can be large, so a tick that cannot do anything does nothing: the scan runs only
    // when a call was registered since the last one, a deadline it computed came due, or the peer
    // rescheduled its reconnect. Known deadlines are slept on exactly; DisconnectCheckPeriod only
    // bounds how late a newly registered call is noticed, since nothing signals its arrival.
    public async Task HandleDisconnect(RpcPeerConnectionState disconnectedState, CancellationToken cancellationToken)
    {
        var whenConnected = disconnectedState.WhenConnected;
        var clientPeer = Peer as RpcClientPeer;
        var clock = clientPeer?.ReconnectDelayer.Clock ?? Peer.Hub.SystemClock;
        var disconnectedAt = clock.Now;
        var checkPeriod = Limits.DisconnectCheckPeriod;
        var newCallTracker = new NewCallTracker();
        var scannedReconnectsAt = (AsyncState<Moment>?)null;
        var nextDeadline = Moment.MaxValue;
        Volatile.Write(ref _newCallTracker, newCallTracker);
        try {
            while (!whenConnected.IsCompleted && !cancellationToken.IsCancellationRequested) {
                var now = clock.Now;
                var reconnectsAt = clientPeer?.ReconnectsAt;
                // TryConsumeNewCalls must run before the scan, and before the cheaper checks - it consumes
                if (newCallTracker.TryConsumeNotification() || now >= nextDeadline || !ReferenceEquals(reconnectsAt, scannedReconnectsAt)) {
                    scannedReconnectsAt = reconnectsAt;
                    nextDeadline = Moment.MaxValue;
                    foreach (var call in Calls.Values) {
                        if (whenConnected.IsCompleted)
                            break; // The peer is back - its response, not our deadline, decides now

                        if (call.ResultTask.IsCompleted)
                            continue;

                        var startedAt = now - call.StartedAt.Elapsed;
                        var since = startedAt > disconnectedAt ? startedAt : disconnectedAt;
                        var timeouts = call.MethodDef.OutboundCallTimeouts;
                        var isConnectDue = IsDue(since, timeouts.ConnectTimeout);
                        // The fallback gets the first refusal, and also gets it when only ConnectTimeout
                        // is due - failing a call that has something to serve would help no one.
                        // A call that serves it is exempt; one that cannot (e.g. NoCache) falls through.
                        if (!call.IsCacheFallbackServed && (isConnectDue || IsDue(since, timeouts.CacheFallbackDelay)))
                            TryHandle(call, static c => c.OnCacheFallbackDelay());
                        if (!call.IsCacheFallbackServed && isConnectDue)
                            TryHandle(call, c => c.OnConnectTimeout(timeouts.ConnectTimeout));
                    }
                }

                using var delayCts = cancellationToken.CreateLinkedTokenSource();
                var delay = TimeSpanExt.Min(nextDeadline - now, checkPeriod.Next());
                var delayTask = Task.Delay(delay.Clamp(TimeSpan.FromMilliseconds(32), TimeSpan.FromDays(1)), delayCts.Token);
                var whenGotNewCall = newCallTracker.WhenNotified;
                var whenAny = reconnectsAt is null
                    ? Task.WhenAny(whenConnected, delayTask, whenGotNewCall)
                    : Task.WhenAny(whenConnected, delayTask, whenGotNewCall, reconnectsAt.WhenNext(delayCts.Token));
                await whenAny.SilentAwait(false);
                delayCts.CancelAndDisposeSilently();
                continue;

                // Due now, or unreachable before the peer next tries to reconnect; otherwise it
                // contributes the wake-up time.
                bool IsDue(Moment since, TimeSpan timeout) {
                    if (timeout == TimeSpanExt.Infinite)
                        return false;

                    var deadline = since + timeout;
                    if (deadline <= now || (reconnectsAt is not null && reconnectsAt.Value > deadline))
                        return true;

                    if (deadline < nextDeadline)
                        nextDeadline = deadline;
                    return false;
                }
            }
        }
        finally {
            Interlocked.CompareExchange(ref _newCallTracker, null, newCallTracker);
        }
        return;

        // A throwing handler must not cost every other call its deadlines
        void TryHandle(RpcOutboundCall call, Action<RpcOutboundCall> handler) {
            try {
                handler.Invoke(call);
            }
            catch (Exception e) {
                Peer.Log.LogError(e, "'{Route}': disconnect handler failed for call {Call}", Peer.Route, call);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool UnregisterLongLiving(RpcOutboundCall call)
        => _longLivingCalls.TryRemove(call.Id, call);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Unregister(RpcOutboundCall call)
        => Calls.TryRemove(call.Id, call);

    public void TryReroute()
    {
        if (!Peer.Route.IsChanged)
            return;

        foreach (var call in this)
            if (call.IsPeerChanged())
                call.SetMustRerouteError();
    }

    public async Task Maintain(RpcPeerConnectionState connectionState, CancellationToken cancellationToken)
    {
        var lastSummaryReportAt = CpuTimestamp.Now;
        var callMetricsPeriod = Peer.Hub.DiagnosticsOptions.OpenCallMetricsPeriodProvider.Invoke(Peer);
        var delayedCallLimit = Limits.LogDelayedCallLimit;
        var summaryLogSettings = Limits.LogCallSummarySettings;
        var keepAliveTimeout = Limits.KeepAliveTimeout;
        var delayHandler = Peer.Hub.OutboundCallOptions.DelayHandler;
        var delayedCalls = new List<RpcOutboundCall>();
        var callsToResend = new List<RpcOutboundCall>();
        try {
            // This loop aborts timed out calls every CallTimeoutCheckPeriod
            while (!cancellationToken.IsCancellationRequested) {
                await Task.Delay(Limits.CallTimeoutCheckPeriod.Next(), cancellationToken).ConfigureAwait(false);

                // When keep-alive is stale, disconnect is imminent (SharedObjects.Maintain handles it).
                // Don't time out calls here — they'll be resent on reconnect.
                // This prevents a race on mobile app resume where this loop could time out calls
                // before the keep-alive disconnect fires and triggers reconnect.
                if (Moment.Now - Peer.LastKeepAliveAt > keepAliveTimeout)
                    continue;

                var callCount = 0;
                var inProgressCallCount = 0;
                var timeoutCallCount = 0;
                var mustRefreshCallMetrics = _lastCallMetricsRefreshAt == default
                    || _lastCallMetricsRefreshAt.Elapsed >= callMetricsPeriod;
                var mustReportInboundCallMetrics = false;
                var mustReportOutboundCallMetrics = false;
                if (mustRefreshCallMetrics) {
                    _lastCallMetricsRefreshAt = CpuTimestamp.Now;
                    mustReportInboundCallMetrics = RpcInstruments.OpenInboundCallGauge.IfEnabled() is not null;
                    mustReportOutboundCallMetrics = RpcInstruments.OpenOutboundCallGauge.IfEnabled() is not null;
                }
                var outboundCallCounts = default(RpcCallStageCounts);
                delayedCalls.Clear();
                callsToResend.Clear();
                foreach (var call in this) {
                    callCount++;
                    if (mustReportOutboundCallMetrics)
                        outboundCallCounts.Add(call.CompletedStage);
                    if (call.ResultTask.IsCompleted)
                        continue;

                    inProgressCallCount++;
                    var timeouts = call.MethodDef.OutboundCallTimeouts;
                    var startedAt = call.StartedAt;
                    if (startedAt == default)
                        continue; // Something is off: call.StartedAt wasn't set

                    var elapsed = startedAt.Elapsed;
                    if (elapsed >= timeouts.RunTimeout) {
                        timeoutCallCount++;
                        var error = Internal.Errors.CallTimeout(Peer.Ref, timeouts.RunTimeout);
                        call.SetError(error, context: null, assumeCancelled: false);
                        Peer.Log.LogError(error,
                            "'{Route}': call {Call} is timed out ({Elapsed} > {Timeout}), completed stage: {Stage}, routing mode: {RoutingMode}",
                            Peer.Route, call,
                            elapsed.ToShortString(), timeouts.RunTimeout.ToShortString(),
                            call.CompletedStageName, call.Context.RoutingMode);
                    }
                    else if (elapsed >= timeouts.DelayTimeout) {
                        delayedCalls.Add(call);
                        var action = delayHandler.Invoke(call, Peer);

                        if (action.HasFlag(RpcDelayedCallAction.Log) && delayedCalls.Count <= delayedCallLimit)
                            Peer.Log.LogWarning(
                                "'{Route}': call {Call} is delayed ({Elapsed} > {DelayTimeout}), completed stage: {Stage}, routing mode: {RoutingMode}",
                                Peer.Route, call,
                                elapsed.ToShortString(), timeouts.DelayTimeout.ToShortString(),
                                call.CompletedStageName, call.Context.RoutingMode);

                        if (action.HasFlag(RpcDelayedCallAction.Abort)) {
                            // Was CallTimeout (kind Run); a delayed-call abort is now its own kind.
                            var error = Internal.Errors.DelayTimeout(Peer.Ref, timeouts.DelayTimeout);
                            call.SetError(error, context: null, assumeCancelled: false);
                        }
                        else if (action.HasFlag(RpcDelayedCallAction.Resend))
                            callsToResend.Add(call);
                    }
                }

#if false // Ugly debugging piece
                if (delayedCalls.Count > 0)
                    WriteLine(
                        $"--- {Peer.Route}: {Peer.ConnectionState.Value.Handshake}, "
                        + $"delayed calls ({delayedCalls.Count}: "
                        + $"{delayedCalls.Select(x => x.MethodDef).ToDelimitedString()}");
#endif
                if (delayedCalls.Count > delayedCallLimit) {
                    Peer.Log.LogWarning(
                        "'{Route}': {UnloggedDelayedCallCount} more delayed call(s) aren't logged",
                        Peer.Route, delayedCalls.Count - delayedCallLimit);
                }

                // Resend delayed calls if requested by the handler
                if (callsToResend.Count > 0 && Peer.Transport is { } transport) {
                    foreach (var call in callsToResend)
                        call.SendRegistered(transport);
                }

                RpcInstruments.RegisterClientCallEvents(
                    delayedCalls.Count, callsToResend.Count, timeoutCallCount);

                if (mustReportInboundCallMetrics || mustReportOutboundCallMetrics) {
                    ReportCallMetrics(
                        mustReportInboundCallMetrics,
                        mustReportOutboundCallMetrics,
                        outboundCallCounts);
                }

                if (lastSummaryReportAt.Elapsed > summaryLogSettings.Period
                    && callCount > summaryLogSettings.MinCount) {
                    lastSummaryReportAt = CpuTimestamp.Now;
                    Peer.Log.LogInformation(
                        "'{Route}': Tracking {CallCount} outbound calls (in progress: {InProgressCallCount}, delayed: {DelayedCallCount})",
                        Peer.Route, callCount, inProgressCallCount, delayedCalls.Count);
                }

                delayedCalls.Clear();
                callsToResend.Clear();
            }
        }
        catch {
            // Intended
        }
    }

    internal void ClearInboundCallMetrics()
    {
        RpcInstruments.UpdateOpenInboundCallCounts(_reportedInboundCallCounts, default);
        _reportedInboundCallCounts = default;
    }

    internal void ClearCallMetrics()
    {
        ClearInboundCallMetrics();
        RpcInstruments.UpdateOpenOutboundCallCounts(_reportedOutboundCallCounts, default);
        _reportedOutboundCallCounts = default;
    }

    public async Task Reconnect(
        RpcPeerConnectionState connectionState,
        List<RpcOutboundCall> sentCalls,
        bool isPeerChanged,
        CancellationToken cancellationToken)
    {
        try {
            // Abort calls that shouldn't survive reconnection based on their RemoteExecutionMode.
            // sentCalls is pruned in place - the caller never reuses it.
            for (var i = sentCalls.Count - 1; i >= 0; i--) {
                var call = sentCalls[i];
                var mode = call.MethodDef.RemoteExecutionMode;
                if (!mode.HasFlag(RpcRemoteExecutionMode.AllowReconnect)) {
                    call.SetError(Internal.Errors.OutboundCallFailedCannotReconnect(connectionState.Error),
                        context: null, assumeCancelled: false);
                    sentCalls.RemoveAt(i);
                }
                else if (isPeerChanged && !mode.HasFlag(RpcRemoteExecutionMode.AllowResend)) {
                    call.SetError(Internal.Errors.OutboundCallFailedCannotResend(connectionState.Error),
                        context: null, assumeCancelled: false);
                    sentCalls.RemoveAt(i);
                }
            }

            if (isPeerChanged) {
                Resend(sentCalls);
                return;
            }

            var failedCalls = await TryReconnect(sentCalls).ConfigureAwait(false);
            Resend(failedCalls);
        }
        catch {
            // Intended
        }
        return;

        void Resend(List<RpcOutboundCall> calls) {
            if (Peer.Transport is not { } transport)
                return;

            foreach (var call in calls) {
                cancellationToken.ThrowIfCancellationRequested();
                if (call.GetReconnectStage(isPeerChanged: true) is not null)
                    call.SendRegistered(transport);
            }
        }

        async Task<List<RpcOutboundCall>> TryReconnect(List<RpcOutboundCall> calls) {
            try {
                var completedStages = calls
                    .Select(call => (
                        Call: call,
                        ReconnectStage: call.GetReconnectStage(isPeerChanged: false)))
                    .Where(x => x.ReconnectStage.HasValue)
                    .GroupBy(x => x.ReconnectStage.GetValueOrDefault(), x => x.Call.Id)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => IncreasingSeqCompressor.Serialize(g.OrderBy(x => x)));
                if (completedStages.Count == 0)
                    return calls; // All calls have to be re-sent

                Task<byte[]> reconnectTask;
                using (new RpcOutboundCallSetup(Peer).Activate()) // No "await" inside this block!
                    reconnectTask = Peer.Hub.SystemCallSender.Client
                        .Reconnect(connectionState.Handshake!.Index, completedStages, cancellationToken);
                var failedCallData = await reconnectTask.ConfigureAwait(false);
                var failedCallIds = IncreasingSeqCompressor.Deserialize(failedCallData).ToHashSet();
                return calls.Where(x => failedCallIds.Contains(x.Id)).ToList();
            }
            catch {
                // If something fails, we re-send every call
                return calls;
            }
        }
    }

    private void ReportCallMetrics(
        bool reportInbound,
        bool reportOutbound,
        RpcCallStageCounts outboundCallCounts)
    {
        if (reportInbound) {
            var inboundCallCounts = Peer.InboundCalls.GetStageCounts();
            RpcInstruments.UpdateOpenInboundCallCounts(_reportedInboundCallCounts, inboundCallCounts);
            _reportedInboundCallCounts = inboundCallCounts;
        }
        if (reportOutbound) {
            RpcInstruments.UpdateOpenOutboundCallCounts(_reportedOutboundCallCounts, outboundCallCounts);
            _reportedOutboundCallCounts = outboundCallCounts;
        }
    }

    public async Task Abort(Exception error, bool assumeCancelled)
    {
        var abortedCallIds = new HashSet<long>();
        for (int i = 0;; i++) {
            var abortedCallCountBefore = abortedCallIds.Count;
            foreach (var call in this) {
                if (abortedCallIds.Add(call.Id))
                    call.SetError(error, context: null, assumeCancelled);
            }
            if (i >= 2 && abortedCallCountBefore == abortedCallIds.Count)
                break;

            await Task.Delay(Limits.CallAbortCyclePeriod).ConfigureAwait(false);
        }
    }

    // Nested types

    private sealed class NewCallTracker
    {
        private readonly Lock _lock = new();
        private volatile bool _isNotified;
        private TaskCompletionSource<Unit> _whenNotified = TaskCompletionSourceExt.New<Unit>();

        public Task WhenNotified => Volatile.Read(ref _whenNotified).Task;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Notify()
        {
            if (_isNotified) return;
            lock (_lock) { // Double-checked locking
                if (_isNotified) return;

                _isNotified = true;
                _whenNotified.TrySetResult(default);
            }
        }

        public bool TryConsumeNotification()
        {
            if (!_isNotified) return false;
            lock (_lock) { // Double-checked locking
                if (!_isNotified) return false;

                _whenNotified = TaskCompletionSourceExt.New<Unit>();
                _isNotified = false;
                return true;
            }
        }
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct RpcCallStageCounts
{
    public int Pending;
    public int ResultReady;
    public int Invalidated;

    public void Add(int completedStage)
    {
        if (completedStage <= 0)
            Pending++;
        else if (completedStage == RpcCallStage.ResultReady)
            ResultReady++;
        else
            Invalidated++;
    }
}
