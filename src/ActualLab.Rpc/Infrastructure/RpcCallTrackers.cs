using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;
using ActualLab.Rpc.Internal;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

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

    public TRpcCall? Get(long callId)
        => Calls.GetValueOrDefault(callId);
}

public sealed class RpcInboundCallTracker : RpcCallTracker<RpcInboundCall>
{
    public RpcInboundCall GetOrRegister(RpcInboundCall call)
    {
        if (call.NoWait || Calls.TryAdd(call.Id, call))
            return call;

        // We could use this call earlier, but it's more expensive,
        // and we should rarely land here, so we do this separately
        return Calls.GetOrAdd(call.Id, static (_, call1) => call1, call);
    }

    public bool Unregister(RpcInboundCall call)
        // NoWait should always return true here!
        => call.NoWait || Calls.TryRemove(call.Id, call);

    public void Clear()
        => Calls.Clear();
}

public sealed class RpcOutboundCallTracker : RpcCallTracker<RpcOutboundCall>
{
    private readonly ConcurrentDictionary<long, RpcOutboundCall> _inProgressCalls = new(HardwareInfo.ProcessorCountPo2, 131);
    private long _lastId;

    public int InProgressCallCount => _inProgressCalls.Count;
    public IEnumerable<RpcOutboundCall> InProgressCalls => _inProgressCalls.Values;

    public void Register(RpcOutboundCall call)
    {
        if (call.NoWait)
            throw new ArgumentOutOfRangeException(nameof(call), "call.NoWait == true.");
        if (call.Id != 0)
            throw new ArgumentOutOfRangeException(nameof(call), "call.Id != 0.");

        call.Id = Interlocked.Increment(ref _lastId);
        call.StartedAt = CpuTimestamp.Now;
        Calls.TryAdd(call.Id, call); // Must succeed for unique call.Id
            _inProgressCalls.TryAdd(call.Id, call);  // Must succeed for unique call.Id
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompleteKeepRegistered(RpcOutboundCall call)
        => _inProgressCalls.TryRemove(call.Id, call);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Unregister(RpcOutboundCall call)
        => Calls.TryRemove(call.Id, call);

    public void TryReroute()
    {
        if (!Peer.Ref.IsRerouted)
            return;

        foreach (var call in this)
            if (call.IsPeerChanged())
                call.SetRerouteError();
    }

    public async Task Maintain(RpcHandshake handshake, CancellationToken cancellationToken)
    {
        var lastSummaryReportAt = CpuTimestamp.Now;
        try {
            // This loop aborts timed out calls every CallTimeoutCheckPeriod
            while (!cancellationToken.IsCancellationRequested) {
                var callCount = 0;
                var inProgressCallCount = 0;
                var delayedCallCount = 0;
                foreach (var call in this) {
                    callCount++;
                    if (call.ResultTask.IsCompleted)
                        continue;

                    inProgressCallCount++;
                    var timeouts = call.MethodDef.Timeouts;
                    var startedAt = call.StartedAt;
                    if (startedAt == default)
                        continue; // Something is off: call.StartedAt wasn't set

                    var elapsed = startedAt.Elapsed;
                    if (elapsed <= timeouts.Timeout)
                        continue;

                    Exception? error = null;
                    // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                    if ((timeouts.TimeoutAction & RpcCallTimeoutAction.Throw) != 0) {
                        error = Internal.Errors.CallTimeout(Peer.Ref, timeouts.Timeout);
                        call.SetError(error, context: null, assumeCancelled: false);
                    }
                    else {
                        // Reset StartedAt to make sure it won't pop up every time we're here
                        call.StartedAt = CpuTimestamp.Now;
                    }
                    // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
                    if ((timeouts.TimeoutAction & RpcCallTimeoutAction.Log) != 0) {
                        if (error is not null)
                            Peer.Log.LogError(error,
                                "{PeerRef}': call {Call} is timed out ({Elapsed} > {Timeout})",
                                Peer.Ref, call, elapsed.ToShortString(), timeouts.Timeout.ToShortString());
                        else if (++delayedCallCount <= RpcCallTimeouts.DelayedCallLogLimit)
                            Peer.Log.LogWarning(
                                "{PeerRef}': call {Call} is delayed ({Elapsed} from its start or prev. report here)",
                                Peer.Ref, call, elapsed.ToShortString());
                    }
                }
                if (delayedCallCount > RpcCallTimeouts.DelayedCallLogLimit)
                    Peer.Log.LogWarning(
                        "{PeerRef}': {UnloggedDelayedCallCount} more delayed call(s) aren't logged",
                        Peer.Ref, delayedCallCount - RpcCallTimeouts.DelayedCallLogLimit);

                var summaryLogSettings = Limits.LogOutboundCallSummarySettings;
                if (lastSummaryReportAt.Elapsed > summaryLogSettings.Period
                    && callCount > summaryLogSettings.MinCount) {
                    lastSummaryReportAt = CpuTimestamp.Now;
                    Peer.Log.LogInformation(
                        "{PeerRef}': Tracking {CallCount} outbound calls (in progress: {InProgressCallCount}, delayed: {DelayedCallCount})",
                        Peer.Ref, callCount, inProgressCallCount, delayedCallCount);
                }

                await Task.Delay(Limits.CallTimeoutCheckPeriod.Next(), cancellationToken).ConfigureAwait(false);
            }
        }
        catch {
            // Intended
        }
    }

    public async Task Reconnect(RpcHandshake handshake, bool isPeerChanged, CancellationToken cancellationToken)
    {
        try {
            var calls = Calls.Values.ToList();
            if (isPeerChanged || handshake.ProtocolVersion < 1) {
                await Resend(calls).ConfigureAwait(false);
                return;
            }

            var failedCalls = await TryReconnect(calls).ConfigureAwait(false);
            await Resend(failedCalls).ConfigureAwait(false);
        }
        catch {
            // Intended
        }
        return;

        async Task Resend(List<RpcOutboundCall> calls) {
            foreach (var call in calls) {
                cancellationToken.ThrowIfCancellationRequested();
                if (call.GetReconnectStage(true) is not null)
                    await call.SendRegistered(false).ConfigureAwait(false);
            }
        }

        async Task<List<RpcOutboundCall>> TryReconnect(List<RpcOutboundCall> calls) {
            try {
                var completedStages = (
                    from call in calls
                    let reconnectStage = call.GetReconnectStage(false)
                    where reconnectStage.HasValue
                    group call.Id by reconnectStage.GetValueOrDefault()
                    into g
                    orderby g.Key
                    select g
                ).ToDictionary(x => x.Key, IncreasingSeqCompressor.Serialize);
                if (completedStages.Count == 0)
                    return calls;

                Task<byte[]> reconnectTask;
                using (new RpcOutboundContext(Peer).Activate())
                    reconnectTask = Peer.Hub.SystemCallSender.Client
                        .Reconnect(handshake.Index, completedStages, cancellationToken);
                var failedCallData = await reconnectTask.ConfigureAwait(false);
                var failedCallIds = IncreasingSeqCompressor.Deserialize(failedCallData).ToHashSet();
                return calls.Where(x => failedCallIds.Contains(x.Id)).ToList();
            }
            catch {
                // If something fails, we fall back to Resend for every call
                return calls;
            }
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
}
