using ActualLab.Fusion.Operations.Internal;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record InvalidationConventionService_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] string Value
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record InvalidationConventionService_Remove(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key
) : ICommand<Unit>;

// Reference implementation of the command-to-query invalidation convention documented in
// docs/PartO.md ("Testing Invalidation"): every mutating command handler must invalidate BOTH
// the entity-specific query it directly targets and every aggregate query whose result may
// change -- dependency propagation only handles transitive dependants of calls a handler
// actually re-triggers, it can't discover an omitted root call on its own.
public class InvalidationConventionService : IComputeService
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    // Entity-specific query
    [ComputeMethod]
    public virtual Task<string?> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    // Aggregate query
    [ComputeMethod]
    public virtual Task<int> Count(CancellationToken cancellationToken = default)
        => Task.FromResult(_values.Count);

    [CommandHandler]
    public virtual Task Set(InvalidationConventionService_Set command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive) {
            _ = Get(command.Key, default);
            _ = Count(default);
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        _values[command.Key] = command.Value;
        return Task.CompletedTask;
    }

    [CommandHandler]
    public virtual Task Remove(InvalidationConventionService_Remove command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive) {
            _ = Get(command.Key, default);
            _ = Count(default);
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        _values.TryRemove(command.Key, out _);
        return Task.CompletedTask;
    }
}

// Same shape as InvalidationConventionService, but Set's invalidation branch "forgets" to
// invalidate the aggregate Count query -- exactly the class of bug the convention test guards
// against. Used by InvalidationConventionTest to prove the pattern actually catches it.
public class BrokenInvalidationConventionService : IComputeService
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    [ComputeMethod]
    public virtual Task<string?> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_values.GetValueOrDefault(key));

    [ComputeMethod]
    public virtual Task<int> Count(CancellationToken cancellationToken = default)
        => Task.FromResult(_values.Count);

    [CommandHandler]
    public virtual Task Set(InvalidationConventionService_Set command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive) {
            _ = Get(command.Key, default);
            // Missing "_ = Count(default)" here on purpose -- see the type doc above.
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        _values[command.Key] = command.Value;
        return Task.CompletedTask;
    }
}
