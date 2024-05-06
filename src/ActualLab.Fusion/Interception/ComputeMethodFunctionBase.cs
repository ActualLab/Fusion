namespace ActualLab.Fusion.Interception;

public interface IComputeMethodFunction : IComputeFunction;

public abstract class ComputeMethodFunctionBase<T>(
    ComputeMethodDef methodDef,
    IServiceProvider services
    ) : ComputeFunctionBase<T>(methodDef, services), IComputeMethodFunction
{
    protected override async ValueTask<Computed<T>> Compute(
        ComputedInput input, Computed<T>? existing,
        CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        if (typedInput.IsDisposed) {
            // Compute takes indefinitely long for disposed compute service's methods
            await TaskExt.NewNeverEndingUnreferenced().WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            var computed = CreateComputed(typedInput);
            try {
                using var _ = Computed.BeginCompute(computed);
                var result = typedInput.InvokeOriginalFunction(cancellationToken);
                if (typedInput.MethodDef.ReturnsValueTask) {
                    var output = await ((ValueTask<T>)result).ConfigureAwait(false);
                    computed.TrySetOutput(output);
                }
                else {
                    var output = await ((Task<T>)result).ConfigureAwait(false);
                    computed.TrySetOutput(output);
                }
                return computed;
            }
            catch (Exception e) {
                if (cancellationToken.IsCancellationRequested) {
                    computed.Invalidate(true); // Instant invalidation on cancellation
                    computed.TrySetOutput(Result.Error<T>(e));
                    if (e is OperationCanceledException)
                        throw;

                    cancellationToken.ThrowIfCancellationRequested(); // Always throws here
                }

                var cancellationReprocessingOptions = typedInput.MethodDef.ComputedOptions.CancellationReprocessing;
                if (e is not OperationCanceledException
                    || ++tryIndex > cancellationReprocessingOptions.MaxTryCount
                    || startedAt.Elapsed > cancellationReprocessingOptions.MaxDuration) {
                    computed.TrySetOutput(Result.Error<T>(e));
                    return computed;
                }

                computed.Invalidate(true); // Instant invalidation on cancellation
                computed.TrySetOutput(Result.Error<T>(e));
                var delay = cancellationReprocessingOptions.RetryDelays[tryIndex];
                Log.LogWarning(e,
                    "Compute #{TryIndex} for {Category} was cancelled internally, retry in {Delay}",
                    tryIndex, typedInput.Category, delay.ToShortString());
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    protected abstract Computed<T> CreateComputed(ComputeMethodInput input);
}
