using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Tests.DbModel;
using ActualLab.Interception;
using ActualLab.Reflection;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DbLocalInvalidationModeService_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

// InvalidationMode.Replicated requires a stored operation to carry its recorded calls, so unlike
// the other InvalidationMode test services this one has to run on a real DbOperationScope.
[DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
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
        DeferredInvalidationModeService_Set command, CancellationToken cancellationToken = default)
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

// Same shape, but Local: its operation carries nothing another host needs, so an auto-resolved
// StoreMode should keep it out of the operation log
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
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

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DbLocalPlusManualCall_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

// Local, but it also records a call by hand. Every host that reads the operation applies those
// recorded calls, so the origin has to as well - it used to be the one host that didn't.
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class DbLocalPlusManualCallService(IServiceProvider services)
    : DbServiceBase<TestDbContext>(services), IComputeService
{
    private readonly ConcurrentDictionary<string, int> _values = new(StringComparer.Ordinal);

    [ComputeMethod]
    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    [ComputeMethod]
    public virtual Task<int> Count(CancellationToken cancellationToken = default)
        => Task.FromResult(_values.Count);

    [CommandHandler]
    public virtual async Task OnSet(
        DbLocalPlusManualCall_Set command, CancellationToken cancellationToken = default)
    {
        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        _values[command.Key] = command.Value;
        // The block names Get; Count is named by hand instead
        CommandContext.GetCurrent().Operation.AddInvalidationCall(NewCountCall());
        Invalidation.Defer(() => _ = Get(command.Key, default));
    }

    public static ServiceCall NewCountCall()
    {
        var type = typeof(DbLocalPlusManualCallService);
        var method = MethodInfoExt.GetByRpcStyleName(type, "Count:1");
        return ServiceCall.New(type, method, ArgumentList.New(CancellationToken.None));
    }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DbDistributedInvalidationModeService_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

// Same shape as DbInvalidationModeService, but Distributed: its calls travel in an event row
// that one host replays, instead of an operation row every host reads.
[DeferredInvalidationMode(DeferredInvalidationMode.Distributed)]
public class DbDistributedInvalidationModeService(IServiceProvider services)
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

    [CommandHandler]
    public virtual async Task OnSet(
        DbDistributedInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        Interlocked.Increment(ref _mutationCount);
        _values[command.Key] = command.Value;
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = Count(default);
        });
    }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DbProtectedInvalidationService_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

// Registered as its interface, but GetInternal is protected - so it isn't on that interface, and
// a record naming it couldn't describe the method's arguments
[DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
public interface IDbProtectedInvalidationService : IComputeService
{
    [ComputeMethod]
    public Task<int> Get(string key, CancellationToken cancellationToken = default);
    // Lets a test capture the protected method's computed
    public Task<int> GetInternalAsPublic(string key, CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task OnSet(DbProtectedInvalidationService_Set command, CancellationToken cancellationToken = default);
}

public class DbProtectedInvalidationService(IServiceProvider services)
    : DbServiceBase<TestDbContext>(services), IDbProtectedInvalidationService
{
    private readonly ConcurrentDictionary<string, int> _values = new(StringComparer.Ordinal);

    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    public Task<int> GetInternalAsPublic(string key, CancellationToken cancellationToken = default)
        => GetInternal(key, cancellationToken);

    [ComputeMethod]
    protected virtual Task<int> GetInternal(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key) * 2);

    [CommandHandler]
    public virtual async Task OnSet(
        DbProtectedInvalidationService_Set command, CancellationToken cancellationToken = default)
    {
        var dbContext = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _1 = dbContext.ConfigureAwait(false);

        _values[command.Key] = command.Value;
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = GetInternal(command.Key, default);
        });
    }
}

// The shape SandboxedKeyValueStore has: a handler that declares no mode and only delegates to a
// Replicated one, so the operation's mode has to come from the nested command
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DbDelegatingOverReplicated_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

public class DbDelegatingOverReplicatedService(ICommander commander) : IComputeService
{
    [CommandHandler]
    public virtual Task OnSet(DbDelegatingOverReplicated_Set command, CancellationToken cancellationToken = default)
        => commander.Call(
            new DeferredInvalidationModeService_Set(command.Key, command.Value), cancellationToken);
}
