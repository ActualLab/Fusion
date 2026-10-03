using System.Diagnostics;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// The Fusion half of <see cref="OperationCompletionHandler"/>: it supplies the one thing the base
/// handler can't, which is what applying an <see cref="Operation.InvalidationCalls"/> entry means.
/// </summary>
/// <remarks>
/// An invalidation call is applied locally right after its operation commits, and on every other
/// host once the operation log delivers the operation.
/// </remarks>
public class FusionOperationCompletionHandler(IServiceProvider services)
    : OperationCompletionHandler(services)
{
    protected RpcHub RpcHub => field ??= Services.GetRequiredService<RpcHub>();

    // A routed pass has no second chance, so it fails loudly; a local one is best-effort
    public override Task ApplyInvalidations(
        IReadOnlyList<ServiceCall> invalidationCalls,
        bool handleLocally,
        CancellationToken cancellationToken = default)
    {
        if (invalidationCalls.Count == 0)
            return Task.CompletedTask;

        return handleLocally
            ? ApplyLocalInvalidations(invalidationCalls)
            : ApplyRoutedInvalidations(invalidationCalls, cancellationToken);
    }

    // Every host applies these to its own cache, so nothing may leave the process
    public async Task ApplyLocalInvalidations(IReadOnlyList<ServiceCall> calls)
    {
        if (calls.Count == 0)
            return;

        var source = new InvalidationSource($"local invalidation: {calls.Count} call(s)");
        using var pass = new InvalidationPass("local", source, calls.Count);
        using var _ = Invalidation.Begin(source);
        foreach (var call in calls) {
            // Forces local execution of any distributed service method. Per call and disposed
            // before the await on purpose: the setup is [ThreadStatic] and ProduceContext consumes
            // it, so one activation would pin only the first call.
            Task<bool> whenApplied;
            using (new RpcOutboundCallSetup(RpcHub.LocalPeer).Activate())
                whenApplied = TryApplyInvalidation(call);
            if (!await whenApplied.ConfigureAwait(false))
                pass.RegisterFailure();
        }
    }

    // One host applies these for the whole mesh, so each one goes to the host that owns its value.
    // Failures propagate: the caller is either the origin (which then leaves the recovery event for
    // someone else) or the recovery itself (which must not mark the event processed).
    public async Task ApplyRoutedInvalidations(
        IReadOnlyList<ServiceCall> calls,
        CancellationToken cancellationToken = default)
    {
        if (calls.Count == 0)
            return;

        var source = new InvalidationSource($"routed invalidation: {calls.Count} call(s)");
        using var pass = new InvalidationPass("routed", source, calls.Count);
        try {
            using var _ = new ComputeContext(CallOptions.RouteInvalidation, source).Activate();
            foreach (var call in calls)
                await ApplyInvalidation(call, mustResolve: true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) {
            pass.RegisterFailure(e);
            throw;
        }
    }

    // Protected methods

    protected virtual async Task<bool> TryApplyInvalidation(ServiceCall call)
    {
        try {
            await ApplyInvalidation(call).ConfigureAwait(false);
            return true;
        }
        catch (Exception e) {
            FusionInstruments.DeferredInvalidationFailureCount.Add(1);
            Log.LogError(e, "Invalidation call failed: {Call}", call);
            return false;
        }
    }

    // Keeps the base's drop reporting, but counts it for the deferred-invalidation metrics too
    protected override void DropInvalidation(ServiceCall call, string reason, bool mustResolve = false)
    {
        FusionInstruments.DeferredInvalidationDropCount.Add(1);
        base.DropInvalidation(call, reason, mustResolve);
    }

    // Nested types

    // kind: "local" or "routed" - the only tag a pass adds to its metrics, because an
    // InvalidationSource names an operation or a command and would blow up the cardinality
    protected sealed class InvalidationPass : IDisposable
    {
        private readonly string _kind;
        private readonly int _callCount;
        private readonly CpuTimestamp _startedAt;
        private readonly Activity? _activity;
        private int _failureCount;

        public InvalidationPass(string kind, InvalidationSource source, int callCount)
        {
            _kind = kind;
            _callCount = callCount;
            _startedAt = CpuTimestamp.Now;
            _activity = FusionInstruments.ActivitySource.StartActivity($"inv.pass.{kind}");
            _activity?.SetTag("invalidation.source", source.ToString());
        }

        public void RegisterFailure(Exception? error = null)
        {
            _failureCount++;
            if (error is not null)
                _activity?.Finalize(error, detectCancellation: true);
        }

        public void Dispose()
        {
            var outcome = _failureCount == 0 ? "success" : "error";
            if (_activity is not null && _failureCount != 0) {
                _activity.SetTag("invalidation.partial_failure", true);
                _activity.SetTag("invalidation.failure.count", _failureCount);
                // A local pass swallows its failures, so RegisterFailure() got no exception to
                // finalize the activity with - but the pass still ended in an error
                if (_activity.Status is ActivityStatusCode.Unset)
                    _activity.SetStatus(ActivityStatusCode.Error);
            }
            var tags = new TagList {
                { "invalidation.kind", _kind },
                { "outcome", outcome },
            };
            FusionInstruments.InvalidationPassDuration.IfEnabled()?.Record(_startedAt.Elapsed.TotalMilliseconds, tags);
            FusionInstruments.InvalidationPassCallCount.IfEnabled()?.Record(_callCount, tags);
            _activity?.Dispose();
        }
    }
}
