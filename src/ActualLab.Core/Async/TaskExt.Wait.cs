using ActualLab.Internal;

namespace ActualLab.Async;

#pragma warning disable MA0004
#pragma warning disable CA2007

public static partial class TaskExt
{
    // WaitAsync

#if !NET6_0_OR_GREATER
    public static Task WaitAsync(
        this Task task,
        CancellationToken cancellationToken = default)
        => task.WaitAsync(MomentClockSet.Default.CpuClock, Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task WaitAsync(
        this Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        => task.WaitAsync(MomentClockSet.Default.CpuClock, timeout, cancellationToken);
#endif

    public static Task WaitAsync(
        this Task task,
        MomentClock clock,
        CancellationToken cancellationToken = default)
        => task.WaitAsync(clock, Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task WaitAsync(
        this Task task,
        MomentClock clock,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (task.IsCompleted)
            return task;
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        return timeout == Timeout.InfiniteTimeSpan
            ? cancellationToken.CanBeCanceled ? WaitForCancellation() : task
            : WaitForTimeout();

        async Task WaitForCancellation() {
            using var dTask = cancellationToken.ToTask();
            var winnerTask = await Task.WhenAny(task, dTask.Resource).ConfigureAwait(false);
            await winnerTask.ConfigureAwait(false);
        }

        async Task WaitForTimeout() {
            var cts = cancellationToken.CreateLinkedTokenSource();
            try {
                var timeoutTask = clock.Delay(timeout, cts.Token);
                var winnerTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
                if (winnerTask == timeoutTask) {
                    // It's a timeoutTask, and there are just two reasons it can be completed:
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException();
                }
                await task.ConfigureAwait(false);
            }
            finally {
                cts.CancelAndDisposeSilently();
            }
        }
    }

#if !NET6_0_OR_GREATER
    public static Task<T> WaitAsync<T>(
        this Task<T> task,
        CancellationToken cancellationToken = default)
        => task.WaitAsync(MomentClockSet.Default.CpuClock, Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task<T> WaitAsync<T>(
        this Task<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        => task.WaitAsync(MomentClockSet.Default.CpuClock, timeout, cancellationToken);
#endif

    public static Task<T> WaitAsync<T>(
        this Task<T> task,
        MomentClock clock,
        CancellationToken cancellationToken = default)
        => task.WaitAsync(clock, Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task<T> WaitAsync<T>(
        this Task<T> task,
        MomentClock clock,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (task.IsCompleted)
            return task;
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        return timeout == Timeout.InfiniteTimeSpan
            ? cancellationToken.CanBeCanceled ? WaitForCancellation() : task
            : WaitForTimeout();

        async Task<T> WaitForCancellation() {
            using var dTask = cancellationToken.ToTask();
            var winnerTask = await Task.WhenAny(task, dTask.Resource).ConfigureAwait(false);
            if (winnerTask == dTask.Resource) {
                cancellationToken.ThrowIfCancellationRequested();
                throw Errors.InternalError("This method can't get here.");
            }
            return await task.ConfigureAwait(false);
        }

        async Task<T> WaitForTimeout()
        {
            var cts = cancellationToken.CreateLinkedTokenSource();
            try {
                var timeoutTask = clock.Delay(timeout, cts.Token);
                var winnerTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
                if (winnerTask == timeoutTask) {
                    // It's a timeoutTask, and there are just two reasons it can be completed:
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException();
                }
                return await task.ConfigureAwait(false);
            }
            finally {
                cts.CancelAndDisposeSilently();
            }
        }
    }

    // WaitResultAsync

    public static Task<Result<Unit>> WaitResultAsync(
        this Task task,
        CancellationToken cancellationToken = default)
        => task.WaitResultAsync(MomentClockSet.Default.CpuClock, Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task<Result<Unit>> WaitResultAsync(
        this Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        => task.WaitResultAsync(MomentClockSet.Default.CpuClock, timeout, cancellationToken);

    public static Task<Result<Unit>> WaitResultAsync(
        this Task task,
        MomentClock clock,
        CancellationToken cancellationToken = default)
        => task.WaitResultAsync(clock, Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task<Result<Unit>> WaitResultAsync(
        this Task task,
        MomentClock clock,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (task.IsCompleted)
            return task.ToResultAsync();
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(new Result<Unit>(default!, new OperationCanceledException(cancellationToken)));

        return timeout == Timeout.InfiniteTimeSpan
            ? cancellationToken.CanBeCanceled ? WaitForCancellation() : task.ToResultAsync()
            : WaitForTimeout();

        async Task<Result<Unit>> WaitForCancellation() {
            using var dTask = cancellationToken.ToTask();
            var winnerTask = await Task.WhenAny(task, dTask.Resource).ConfigureAwait(false);
            return winnerTask.ToResultSynchronously();
        }

        async Task<Result<Unit>> WaitForTimeout()
        {
            var cts = cancellationToken.CreateLinkedTokenSource();
            try {
                var timeoutTask = clock.Delay(timeout, cts.Token);
                var winnerTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
                if (winnerTask == timeoutTask)
                    return cancellationToken.IsCancellationRequested
                        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                        ? new Result<Unit>(default!, new OperationCanceledException(cancellationToken))
                        : new Result<Unit>(default!, new TimeoutException());
                return task.ToResultSynchronously();
            }
            finally {
                cts.CancelAndDisposeSilently();
            }
        }
    }

    public static Task<Result<T>> WaitResultAsync<T>(
        this Task<T> task,
        CancellationToken cancellationToken = default)
        => task.WaitResultAsync(MomentClockSet.Default.CpuClock, Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task<Result<T>> WaitResultAsync<T>(
        this Task<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
        => task.WaitResultAsync(MomentClockSet.Default.CpuClock, timeout, cancellationToken);

    public static Task<Result<T>> WaitResultAsync<T>(
        this Task<T> task,
        MomentClock clock,
        CancellationToken cancellationToken = default)
        => task.WaitResultAsync(clock, Timeout.InfiniteTimeSpan, cancellationToken);

    public static Task<Result<T>> WaitResultAsync<T>(
        this Task<T> task,
        MomentClock clock,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (task.IsCompleted)
            return task.ToResultAsync();
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(new Result<T>(default!, new OperationCanceledException(cancellationToken)));

        return timeout == Timeout.InfiniteTimeSpan
            ? cancellationToken.CanBeCanceled ? WaitForCancellation() : task.ToResultAsync()
            : WaitForTimeout();

        async Task<Result<T>> WaitForCancellation() {
            using var dTask = cancellationToken.ToTask();
            var winnerTask = await Task.WhenAny(task, dTask.Resource).ConfigureAwait(false);
            return winnerTask == task
                ? task.ToResultSynchronously()
                : new Result<T>(default!, new OperationCanceledException(cancellationToken));
        }

        async Task<Result<T>> WaitForTimeout()
        {
            var cts = cancellationToken.CreateLinkedTokenSource();
            try {
                var timeoutTask = clock.Delay(timeout, cts.Token);
                var winnerTask = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
                if (winnerTask == timeoutTask)
                    return cancellationToken.IsCancellationRequested
                        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                        ? new Result<T>(default!, new OperationCanceledException(cancellationToken))
                        : new Result<T>(default!, new TimeoutException());
                return task.ToResultSynchronously();
            }
            finally {
                cts.CancelAndDisposeSilently();
            }
        }
    }
}
