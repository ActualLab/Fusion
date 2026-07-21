using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc;

/// <summary>
/// A single route generation of an <see cref="RpcRef"/>: carries the resolved target info
/// for this generation and signals when a route change (reroute) occurs.
/// A static route (see <see cref="NewStatic"/>) belongs to a ref that never reroutes.
/// </summary>
public class RpcRoute : IEquatable<RpcRoute>
{
    private static readonly Func<bool, CancellationToken, ValueTask> FinalLocalExecutionAwaiter
        = static (_, _) => throw RpcRerouteException.MustReroute();
    private static readonly Task NeverEndingTask = TaskExt.NewNeverEndingUnreferenced();

    private readonly TaskCompletionSource<Unit>? _changedSource;
    private readonly CancellationTokenSource? _changedTokenSource;
    private string? _toString;
    private string? _toStringChanged;

    public RpcRef Ref { get; }
    public int Version { get; }
    public RpcPeerConnectionKind ConnectionKind { get; init; } // None = use RpcPeerOptions.ConnectionKindDetector
    public CancellationToken ChangedToken { get; }
    public Func<bool, CancellationToken, ValueTask>? LocalExecutionAwaiter { get; set; }

    public bool IsStatic => _changedTokenSource is null;
    public bool IsChanged => ChangedToken.IsCancellationRequested;
    public Task WhenChanged => _changedSource?.Task ?? NeverEndingTask;

    public static RpcRoute NewStatic(RpcRef rpcRef)
        => new(rpcRef);

    public RpcRoute(RpcRef rpcRef, CancellationTokenSource? changedTokenSource = null)
    {
        Ref = rpcRef;
        Version = rpcRef.NextRouteVersion();
        _changedSource = TaskCompletionSourceExt.New<Unit>();
        _changedTokenSource = changedTokenSource ?? new CancellationTokenSource();
        ChangedToken = _changedTokenSource.Token;
        ChangedToken.Register(() => _changedSource.TrySetResult(default));
    }

    private RpcRoute(RpcRef rpcRef)
        => Ref = rpcRef;

    public override string ToString()
    {
        if (IsStatic)
            return Ref.ToString();

        return IsChanged
            ? _toStringChanged ??= $"{Ref} [v{Version}x{GetTargetSuffix()}]"
            : _toString ??= $"{Ref} [v{Version}{GetTargetSuffix()}]";

        string GetTargetSuffix() {
            var target = GetTargetString();
            return target.IsNullOrEmpty() ? "" : "->" + target;
        }
    }

    public void MarkChanged()
    {
        if (_changedTokenSource is null)
            throw Errors.InternalError($"A static {nameof(RpcRoute)} cannot be marked as changed.");

        lock (_changedTokenSource) {
            if (ReferenceEquals(LocalExecutionAwaiter, FinalLocalExecutionAwaiter))
                return;

            _changedTokenSource.CancelAndDisposeSilently();
            LocalExecutionAwaiter = FinalLocalExecutionAwaiter;
        }
    }

    public void ThrowIfChanged()
    {
        if (ChangedToken.IsCancellationRequested)
            throw RpcRerouteException.MustReroute();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RerouteIfChanged(string? reason = null)
    {
        if (IsChanged)
            throw RpcRerouteException.MustReroute(reason);
    }

    public ValueTask<CancellationTokenSource?> PrepareLocalExecution(
        RpcMethodDef methodDef, bool addDependency, CancellationToken cancellationToken)
    {
        if (methodDef.LocalExecutionMode == RpcLocalExecutionMode.Unconstrained
            || LocalExecutionAwaiter is not { } localExecutionAwaiter)
            return default;

        var whenReadyTask = localExecutionAwaiter.Invoke(addDependency, cancellationToken);
        if (whenReadyTask.IsCompletedSuccessfully) {
            if (methodDef.LocalExecutionMode == RpcLocalExecutionMode.ConstrainedEntry)
                RerouteIfChanged();
            return default;
        }

        return CompleteAsync(this, methodDef, whenReadyTask, cancellationToken);

        static async ValueTask<CancellationTokenSource?> CompleteAsync(
            RpcRoute route, RpcMethodDef methodDef, ValueTask whenReadyTask,
            CancellationToken cancellationToken)
        {
            await whenReadyTask.ConfigureAwait(false);
            if (methodDef.LocalExecutionMode == RpcLocalExecutionMode.ConstrainedEntry) {
                route.RerouteIfChanged();
                return null;
            }

            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, route.ChangedToken);
        }
    }

    public bool MustConvertToRpcRerouteException(
        OperationCanceledException error,
        CancellationTokenSource? linkedTokenSource,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;
        if (linkedTokenSource is null)
            return false;
        if (error is RpcRerouteException)
            return false;

        return IsChanged;
    }

    // Protected methods

    protected virtual string GetTargetString()
        => "";

    // Equality

    public bool Equals(RpcRoute? other)
        => ReferenceEquals(this, other)
        || (other is not null && other.GetType() == GetType() && Ref.Equals(other.Ref) && Version == other.Version);

    public sealed override bool Equals(object? obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;

        return obj.GetType() == GetType() && Equals((RpcRoute)obj);
    }

    public sealed override int GetHashCode()
        => Ref.GetHashCode() ^ Version;

    public static bool operator ==(RpcRoute? left, RpcRoute? right) => Equals(left, right);
    public static bool operator !=(RpcRoute? left, RpcRoute? right) => !Equals(left, right);
}
