using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Tests.DbModel;

namespace ActualLab.Fusion.Tests.Services;

// InvalidationMode.Replicated requires a stored operation to carry its recorded calls, so unlike
// the other InvalidationMode test services this one has to run on a real DbOperationScope.
[InvalidationMode(InvalidationMode.Replicated)]
public class DbInvalidationModeService(IServiceProvider services)
    : DbServiceBase<TestDbContext>(services), IComputeService
{
    private readonly ConcurrentDictionary<string, int> _values = new(StringComparer.Ordinal);
    private int _mutationCount;

    public int MutationCount => Volatile.Read(ref _mutationCount);

    [ComputeMethod]
    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    [ComputeMethod]
    public virtual Task<int> Count(CancellationToken cancellationToken = default)
        => Task.FromResult(_values.Count);

    [ComputeMethod]
    public virtual Task<int> CountOfLength(int length, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.Keys.Count(x => x.Length == length));

    [CommandHandler]
    public virtual async Task OnSet(
        InvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        Interlocked.Increment(ref _mutationCount);
        _values[command.Key] = command.Value;
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = Count(default);
            _ = CountOfLength(command.Key.Length, default);
        });
    }
}
