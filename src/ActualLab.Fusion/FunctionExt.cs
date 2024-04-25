namespace ActualLab.Fusion;

public static class FunctionExt
{
    public static async ValueTask<IComputed> InvokeSafe(this IFunction function,
        ComputedInput input,
        IComputed? usedBy,
        ComputeContext? context,
        CancellationToken cancellationToken = default)
    {
        ILogger? log = null;
        while (true) {
            try {
                return await function
                    .Invoke(input, null, context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested) {
                log ??= input.Function.Services.LogFor(function.GetType());
                var retryDelay = Computed.InternalCancellationRetryDelay;
                log.LogWarning(e,
                    "Update was cancelled internally for {Category}, will retry in {Delay}",
                    input.Category, retryDelay.ToShortString());
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static async ValueTask<Computed<T>> InvokeSafe<T>(this IFunction<T> function,
        ComputedInput input,
        IComputed? usedBy,
        ComputeContext? context,
        CancellationToken cancellationToken = default)
    {
        ILogger? log = null;
        while (true) {
            try {
                return await function
                    .Invoke(input, null, context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested) {
                log ??= input.Function.Services.LogFor(function.GetType());
                var retryDelay = Computed.InternalCancellationRetryDelay;
                log.LogWarning(e,
                    "Update was cancelled internally for {Category}, will retry in {Delay}",
                    input.Category, retryDelay.ToShortString());
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
