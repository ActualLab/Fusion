using ActualLab.Interception;

namespace ActualLab.Fusion.Interception;

public interface IComputeMethodFunction : IComputeFunction
{
    ComputeMethodDef MethodDef { get; }
    ComputedOptions ComputedOptions { get; }
}

public class ComputeMethodFunction<T>(
    ComputeMethodDef methodDef,
    IServiceProvider services
    ) : ComputeFunctionBase<T>(services), IComputeMethodFunction
{
    public ComputeMethodDef MethodDef { get; } = methodDef;
    public ComputedOptions ComputedOptions { get; } = methodDef.ComputedOptions;

    public override string ToString()
        => MethodDef.FullName;

    protected override async ValueTask<Computed<T>> Compute(
        ComputedInput input, Computed<T>? existing,
        CancellationToken cancellationToken)
    {
        var typedInput = (ComputeMethodInput)input;
        var tryIndex = 0;
        var startedAt = CpuTimestamp.Now;
        while (true) {
            var computed = new ComputeMethodComputed<T>(ComputedOptions, typedInput);
            try {
                using var _ = Computed.BeginCompute(computed);
                var result = InvokeImplementation(typedInput, cancellationToken);
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

    protected static object InvokeImplementation(ComputeMethodInput input, CancellationToken cancellationToken)
    {
        var ctIndex = input.MethodDef.CancellationTokenIndex;
        var invocation = input.Invocation;
        if (ctIndex < 0)
            return invocation.InterceptedUntyped()!;

        var arguments = input.Arguments;
        arguments.SetCancellationToken(ctIndex, cancellationToken);
        try {
            return invocation.InterceptedUntyped()!;
        }
        finally {
            arguments.SetCancellationToken(ctIndex, default); // Otherwise it may cause memory leak
        }
    }
}
