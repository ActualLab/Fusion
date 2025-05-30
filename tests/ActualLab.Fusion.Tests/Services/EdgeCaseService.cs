namespace ActualLab.Fusion.Tests.Services;

public interface IEdgeCaseService : IComputeService
{
    [ComputeMethod(MinCacheDuration = 10)]
    public Task<string> GetSuffix(CancellationToken cancellationToken = default);
    public Task SetSuffix(string suffix, CancellationToken cancellationToken = default);

    [ComputeMethod(MinCacheDuration = 10)]
    public Task<long?> GetNullable(long source, CancellationToken cancellationToken = default);

    [ComputeMethod(MinCacheDuration = 10)]
    public Task<string> ThrowIfContainsError(string source, CancellationToken cancellationToken = default);
    public Task<string> ThrowIfContainsErrorNonCompute(string source, CancellationToken cancellationToken = default);
}

public class EdgeCaseService(StateFactory stateFactory) : IEdgeCaseService
{
    public MutableState<string> SuffixState { get; } = stateFactory.NewMutable<string>();

    public virtual Task<string> GetSuffix(CancellationToken cancellationToken = default)
        => Task.FromResult(SuffixState.Value);

    public Task SetSuffix(string suffix, CancellationToken cancellationToken = default)
    {
        SuffixState.Value = suffix;
        return Task.CompletedTask;
    }

    public virtual Task<long?> GetNullable(long source, CancellationToken cancellationToken = default)
        => Task.FromResult(source != 0 ? (long?) source : null);

    public virtual async Task<string> ThrowIfContainsError(string source, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        return source.ToLowerInvariant().Contains("error")
            ? throw new ArgumentException("Error!", nameof(source))
            : source + await SuffixState.Use(cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<string> ThrowIfContainsErrorRewriteErrors(string source, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        return source.ToLowerInvariant().Contains("error")
            ? throw new ArgumentException("Error!", nameof(source))
            : source + await SuffixState.Use(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ThrowIfContainsErrorNonCompute(string source, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        return source.ToLowerInvariant().Contains("error")
            ? throw new ArgumentException("Error!", nameof(source))
            : source + SuffixState.Value;
    }
}
