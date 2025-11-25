namespace ActualLab.Rpc;

public sealed class RpcRouteState
{
    private static readonly Func<CancellationToken, ValueTask> FinalLocalExecutionAwaiter
        = static _ => throw RpcRerouteException.MustReroute();

    private readonly TaskCompletionSource<Unit> _changedSource;
    private readonly CancellationTokenSource _changedTokenSource;

    public CancellationToken ChangedToken { get; }
    public Func<CancellationToken, ValueTask>? LocalExecutionAwaiter { get; set; }
    public Task WhenChanged => _changedSource.Task;

    public RpcRouteState(CancellationTokenSource? changedTokenSource = null)
    {
        _changedSource = TaskCompletionSourceExt.New<Unit>();
        _changedTokenSource = changedTokenSource ?? new CancellationTokenSource();
        ChangedToken = _changedTokenSource.Token;
        ChangedToken.Register(() => _changedSource.TrySetResult(default));
    }

    public void MarkChanged()
    {
        lock (_changedTokenSource) {
            if (ReferenceEquals(LocalExecutionAwaiter, FinalLocalExecutionAwaiter))
                return;

            _changedTokenSource.CancelAndDisposeSilently();
            LocalExecutionAwaiter = FinalLocalExecutionAwaiter;
        }
    }
}
