using ActualLab.Versioning;

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
        catch (OperationCanceledException e) {
            computed.TrySetOutput(Result.Error<T>(e));
            if (cancellationToken.IsCancellationRequested)
                computed.Invalidate(); // Instant invalidation
            throw;
        }
        catch (Exception e) {
            if (e is AggregateException ae)
                e = ae.GetFirstInnerException();
            computed.TrySetOutput(Result.Error<T>(e));
            // If the output is already set, all we can
            // is to ignore the exception we've just caught;
            // throwing it further will probably make it just worse,
            // since the the caller have to take this scenario into acc.
        }
        return computed;
    }

    protected abstract Computed<T> CreateComputed(ComputeMethodInput input);
}
