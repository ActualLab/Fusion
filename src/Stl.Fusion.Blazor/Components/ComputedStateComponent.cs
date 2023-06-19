using Stl.Fusion.Blazor.Internal;
using Stl.OS;

namespace Stl.Fusion.Blazor;

public static class ComputedStateComponent
{
    private static readonly ConcurrentDictionary<Type, string> StateCategoryCache = new();

    public static ComputedStateComponentOptions DefaultOptions { get; set; }

    static ComputedStateComponent()
    {
        DefaultOptions = ComputedStateComponentOptions.SynchronizeComputeState
            | ComputedStateComponentOptions.RecomputeOnParametersSet;
        if (HardwareInfo.IsSingleThreaded)
           DefaultOptions = ComputedStateComponentOptions.RecomputeOnParametersSet;
    }

    public static string GetStateCategory(Type componentType)
        => StateCategoryCache.GetOrAdd(componentType, static t => $"{t.GetName()}.State");

    public static string GetMutableStateCategory(Type componentType)
        => StateCategoryCache.GetOrAdd(componentType, static t => $"{t.GetName()}.MutableState");
}

public abstract class ComputedStateComponent<TState> : StatefulComponentBase<IComputedState<TState>>
{
    protected ComputedStateComponentOptions Options { get; init; } = ComputedStateComponent.DefaultOptions;

    // State frequently depends on component parameters, so...
    protected override Task OnParametersSetAsync()
    {
        if ((Options & ComputedStateComponentOptions.RecomputeOnParametersSet) == 0)
            return Task.CompletedTask;
        _ = State.Recompute();
        return Task.CompletedTask;
    }

    protected virtual string GetStateCategory()
        => ComputedStateComponent.GetStateCategory(GetType());

    protected virtual ComputedState<TState>.Options GetStateOptions()
        => new() { Category = GetStateCategory() };

    protected override IComputedState<TState> CreateState()
    {
        // Synchronizes ComputeState call as per:
        // https://github.com/servicetitan/Stl.Fusion/issues/202
        Func<IComputedState<TState>, CancellationToken, Task<TState>> computeState =
            (Options & ComputedStateComponentOptions.SynchronizeComputeState) == 0
                ? UnsynchronizedComputeState
                : DispatcherInfo.FlowsExecutionContext(this)
                    ? SynchronizedComputeState
                    : SynchronizedComputeStateWithExecutionContextFlow;
        return StateFactory.NewComputed(GetStateOptions(), computeState);

        Task<TState> UnsynchronizedComputeState(
            IComputedState<TState> state, CancellationToken cancellationToken)
            => ComputeState(cancellationToken);

        Task<TState> SynchronizedComputeState(
            IComputedState<TState> state, CancellationToken cancellationToken)
        {
            var tcs = TaskCompletionSourceExt.New<TState>();
            _ = InvokeAsync(() => {
                try {
                    var computeStateTask = ComputeState(cancellationToken);
                    _ = tcs.TrySetFromTaskAsync(computeStateTask, cancellationToken);
                }
                catch (OperationCanceledException) {
                    if (cancellationToken.IsCancellationRequested)
                        tcs.TrySetCanceled(cancellationToken);
                    else
                        tcs.TrySetCanceled();
                }
                catch (Exception e) {
                    tcs.TrySetException(e);
                }
            });
            return tcs.Task;
        }

        Task<TState> SynchronizedComputeStateWithExecutionContextFlow(
            IComputedState<TState> state, CancellationToken cancellationToken)
        {
            var tcs = TaskCompletionSourceExt.New<TState>();
            var executionContext = ExecutionContext.Capture();
            if (executionContext == null) {
                // Nothing to restore
                _ = InvokeAsync(() => {
                    try {
                        var computeStateTask = ComputeState(cancellationToken);
                        _ = tcs.TrySetFromTaskAsync(computeStateTask, cancellationToken);
                    }
                    catch (OperationCanceledException) {
                        if (cancellationToken.IsCancellationRequested)
                            tcs.TrySetCanceled(cancellationToken);
                        else
                            tcs.TrySetCanceled();
                    }
                    catch (Exception e) {
                        tcs.TrySetException(e);
                    }
                });
            }
            else {
                _ = InvokeAsync(() => {
                    ExecutionContext.Run(executionContext, _1 => {
                        try {
                            var computeStateTask = ComputeState(cancellationToken);
                            _ = tcs.TrySetFromTaskAsync(computeStateTask, cancellationToken);
                        }
                        catch (OperationCanceledException) {
                            if (cancellationToken.IsCancellationRequested)
                                tcs.TrySetCanceled(cancellationToken);
                            else
                                tcs.TrySetCanceled();
                        }
                        catch (Exception e) {
                            tcs.TrySetException(e);
                        }
                    }, null);
                    return tcs.Task;
                });
            }
            return tcs.Task;
        }
    }

    protected abstract Task<TState> ComputeState(CancellationToken cancellationToken);
}
