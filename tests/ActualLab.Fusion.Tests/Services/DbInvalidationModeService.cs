using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Tests.DbModel;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DbLocalInvalidationModeService_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DbLegacyInvalidationModeService_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

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

// Same shape, but Local: its operation carries nothing another host needs, so "auto"
// MustStoreOperation should keep it out of the operation log
[InvalidationMode(InvalidationMode.Local)]
public class DbLocalInvalidationModeService(IServiceProvider services)
    : DbServiceBase<TestDbContext>(services), IComputeService
{
    private readonly ConcurrentDictionary<string, int> _values = new(StringComparer.Ordinal);

    [ComputeMethod]
    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    [CommandHandler]
    public virtual async Task OnSet(
        DbLocalInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        _values[command.Key] = command.Value;
        Invalidation.Defer(() => _ = Get(command.Key, default));
    }
}

// The default mode: its invalidation happens through the replay of the operation log row,
// so "auto" MustStoreOperation has to keep storing it
public class DbLegacyInvalidationModeService(IServiceProvider services)
    : DbServiceBase<TestDbContext>(services), IComputeService
{
    private readonly ConcurrentDictionary<string, int> _values = new(StringComparer.Ordinal);

    [ComputeMethod]
    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    [CommandHandler]
    public virtual async Task OnSet(
        DbLegacyInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive) {
            _ = Get(command.Key, default);
            return;
        }

        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        _values[command.Key] = command.Value;
    }
}
