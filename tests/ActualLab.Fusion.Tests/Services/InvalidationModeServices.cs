using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations;
using ActualLab.Fusion.Operations.Internal;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record InvalidationModeService_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record InvalidationModeService_SetViaNested(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record InvalidationModeService_SetLocal(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

// The shared query surface of every InvalidationMode test service: an entity query, an aggregate
// query, and a query keyed by an int - the last one is what exercises argument coercion when a
// recorded call comes back from its text-serialized form.
public abstract class InvalidationModeServiceBase : IComputeService
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

    // Protected methods

    protected void Mutate(string key, int value)
    {
        Interlocked.Increment(ref _mutationCount);
        _values[key] = value;
    }
}

[InvalidationMode(InvalidationMode.Local)]
public class LocalInvalidationModeService : InvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(InvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        InMemoryOperationScope.Require();
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = Count(default);
            _ = CountOfLength(command.Key.Length, default);
        });
        return Task.CompletedTask;
    }
}

[InvalidationMode(InvalidationMode.Replicated)]
public class ReplicatedInvalidationModeService : InvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(InvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        InMemoryOperationScope.Require();
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = Count(default);
            _ = CountOfLength(command.Key.Length, default);
        });
        return Task.CompletedTask;
    }
}

public class LegacyInvalidationModeService : InvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(InvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive) {
            _ = Get(command.Key, default);
            _ = Count(default);
            _ = CountOfLength(command.Key.Length, default);
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        Mutate(command.Key, command.Value);
        return Task.CompletedTask;
    }
}

// A handler that invalidates nothing itself. OnSetViaNested says so explicitly and lets the nested
// command do the invalidation; OnSet is the "mutates and forgets" case the mode also has to cover.
[InvalidationMode(InvalidationMode.None)]
public class NoneInvalidationModeService(ICommander commander) : InvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(InvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        InMemoryOperationScope.Require();
        Mutate(command.Key, command.Value);
        return Task.CompletedTask;
    }

    [CommandHandler]
    public virtual Task OnSetViaNested(
        InvalidationModeService_SetViaNested command, CancellationToken cancellationToken)
        => commander.Call(new InvalidationModeService_SetLocal(command.Key, command.Value), cancellationToken);

    // The method-level attribute overrides the class-level None
    [CommandHandler]
    [InvalidationMode(InvalidationMode.Local)]
    public virtual Task OnSetLocal(
        InvalidationModeService_SetLocal command, CancellationToken cancellationToken = default)
    {
        InMemoryOperationScope.Require();
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = Count(default);
            _ = CountOfLength(command.Key.Length, default);
        });
        return Task.CompletedTask;
    }
}

// Defer() from a Legacy handler is a declaration/implementation mismatch, and must fail loudly
public class MisdeclaredInvalidationModeService : InvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(InvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        InMemoryOperationScope.Require();
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => _ = Get(command.Key, default));
        return Task.CompletedTask;
    }
}

// Captures completed operations so a test can inspect what the operation record carries
public sealed class OperationCapture : IOperationCompletionListener
{
    private readonly List<Operation> _operations = [];

    public IReadOnlyList<Operation> Operations {
        get {
            lock (_operations)
                return _operations.ToArray();
        }
    }

    public Task OnOperationCompleted(Operation operation, CommandContext? commandContext)
    {
        lock (_operations)
            _operations.Add(operation);
        return Task.CompletedTask;
    }
}
