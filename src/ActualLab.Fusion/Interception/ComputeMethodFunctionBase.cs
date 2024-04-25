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
        var typedInput = (ComputeMethodInput) input;
        var computed = CreateComputed(typedInput);
        try {
            using var _ = Computed.ChangeCurrent(computed);
            var result = typedInput.InvokeOriginalFunction(cancellationToken);
            if (typedInput.MethodDef.ReturnsValueTask) {
                var output = await ((ValueTask<T>) result).ConfigureAwait(false);
                computed.TrySetOutput(output);
            }
            else {
                var output = await ((Task<T>) result).ConfigureAwait(false);
                computed.TrySetOutput(output);
            }
        }
        catch (Exception e) {
            // if (e is AggregateException ae)
            //     e = ae.GetFirstInnerException();
            if (cancellationToken.IsCancellationRequested) {
                computed.Invalidate(true); // Instant invalidation
                computed.TrySetOutput(Result.Error<T>(e));
                throw;
            }
            computed.TrySetOutput(Result.Error<T>(e));
        }
        return computed;
    }

    protected abstract Computed<T> CreateComputed(ComputeMethodInput input);
}
