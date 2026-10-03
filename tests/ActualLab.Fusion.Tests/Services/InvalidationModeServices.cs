using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Operations.Internal;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DeferredInvalidationModeService_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DeferredInvalidationModeService_SetViaNested(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record DeferredInvalidationModeService_SetLocal(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key,
    [property: DataMember, MemoryPackOrder(1), Key(1)] int Value
) : ICommand<Unit>;

// The shared query surface of every InvalidationMode test service: an entity query, an aggregate
// query, and a query keyed by an int - the last one is what exercises argument coercion when a
// recorded call comes back from its text-serialized form.
public abstract class DeferredInvalidationModeServiceBase : IComputeService
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

[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class LocalDeferredDeferredInvalidationModeService : DeferredInvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(DeferredInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        TransientOperationScope.Require();
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = Count(default);
            _ = CountOfLength(command.Key.Length, default);
        });
        return Task.CompletedTask;
    }
}

// A transient operation stores nothing, so there is no reliable way to replicate its invalidation
[DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
public class TransientReplicatedDeferredDeferredInvalidationModeService : DeferredInvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(DeferredInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        TransientOperationScope.Require();
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => _ = Get(command.Key, default));
        return Task.CompletedTask;
    }
}

// Same, but without any operation scope at all
[DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
public class ScopelessReplicatedDeferredDeferredInvalidationModeService : DeferredInvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(DeferredInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => _ = Get(command.Key, default));
        return Task.CompletedTask;
    }
}

// A handler that declares no mode and defers nothing, so it never gets a say in the mode:
// OnSetViaNested leaves that to the nested command, and OnSet is the "mutates and forgets" case
public class DelegatingDeferredInvalidationModeService(ICommander commander) : DeferredInvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(DeferredInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        TransientOperationScope.Require();
        Mutate(command.Key, command.Value);
        return Task.CompletedTask;
    }

    [CommandHandler]
    public virtual Task OnSetViaNested(
        DeferredInvalidationModeService_SetViaNested command, CancellationToken cancellationToken)
        => commander.Call(new DeferredInvalidationModeService_SetLocal(command.Key, command.Value), cancellationToken);

    // The only member here that declares a mode, because it's the only one that defers
    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public virtual Task OnSetLocal(
        DeferredInvalidationModeService_SetLocal command, CancellationToken cancellationToken = default)
    {
        TransientOperationScope.Require();
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => {
            _ = Get(command.Key, default);
            _ = Count(default);
            _ = CountOfLength(command.Key.Length, default);
        });
        return Task.CompletedTask;
    }
}

// Defers a block without declaring a mode, which is an error - there is no default to fall back to
public class UndeclaredDeferredInvalidationModeService : DeferredInvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(DeferredInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        TransientOperationScope.Require();
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => _ = Get(command.Key, default));
        return Task.CompletedTask;
    }
}

// Runs a nested command inside an invalidation pass, which InvalidationGuard rejects
[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class CommandDuringInvalidationService(ICommander commander) : DeferredInvalidationModeServiceBase
{
    [CommandHandler]
    public virtual async Task OnSet(
        DeferredInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        TransientOperationScope.Require();
        Mutate(command.Key, command.Value);
        using var _ = Invalidation.Begin();
        await commander
            .Call(new DeferredInvalidationModeService_SetLocal(command.Key, command.Value), cancellationToken)
            .ConfigureAwait(false);
    }

    [CommandHandler]
    public virtual Task OnSetLocal(
        DeferredInvalidationModeService_SetLocal command, CancellationToken cancellationToken = default)
    {
        Mutate(command.Key, command.Value);
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

// Distributed needs a stored event to recover from, which a transient operation can't provide
[DeferredInvalidationMode(DeferredInvalidationMode.Distributed)]
public class TransientDistributedDeferredInvalidationModeService : DeferredInvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(DeferredInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        TransientOperationScope.Require();
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => _ = Get(command.Key, default));
        return Task.CompletedTask;
    }
}

// Registered in RpcServiceMode.Distributed, which is what puts its calls on
// RemoteComputeMethodFunction even when nothing is remote
public interface IDistributedCounter : IComputeService
{
    [ComputeMethod]
    public Task<int> Get(string key, CancellationToken cancellationToken = default);
}

public class DistributedCounter : IDistributedCounter
{
    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

// Distributed with no operation scope at all: nothing can carry the invalidation off this host
[DeferredInvalidationMode(DeferredInvalidationMode.Distributed)]
public class ScopelessDistributedDeferredInvalidationModeService : DeferredInvalidationModeServiceBase
{
    [CommandHandler]
    public virtual Task OnSet(DeferredInvalidationModeService_Set command, CancellationToken cancellationToken = default)
    {
        Mutate(command.Key, command.Value);
        Invalidation.Defer(() => _ = Get(command.Key, default));
        return Task.CompletedTask;
    }
}

// A service whose command handlers are declared on its interfaces, with the mode on the
// implementation - the shape IAuth/IAuthBackend + DbAuthService has
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record InterfaceDeclaredService_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record InterfaceDeclaredBackend_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key
) : ICommand<Unit>;

public interface IInterfaceDeclaredService : IComputeService
{
    [CommandHandler]
    Task OnSet(InterfaceDeclaredService_Set command, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<int> Get(string key, CancellationToken cancellationToken = default);
}

public interface IInterfaceDeclaredBackend : IComputeService
{
    [CommandHandler]
    Task OnBackendSet(InterfaceDeclaredBackend_Set command, CancellationToken cancellationToken = default);
}

[DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
public class InterfaceDeclaredService : IInterfaceDeclaredService, IInterfaceDeclaredBackend
{
    public virtual Task OnSet(InterfaceDeclaredService_Set command, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task OnBackendSet(InterfaceDeclaredBackend_Set command, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task<int> Get(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

// A handler that nests two commands declaring different modes - the conflict one carrier can't express
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record MixedModes_Set(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record MixedModes_Local(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key
) : ICommand<Unit>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record MixedModes_Replicated(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Key
) : ICommand<Unit>;

public class MixedModesService(ICommander commander) : DeferredInvalidationModeServiceBase
{
    [CommandHandler]
    public virtual async Task OnSet(MixedModes_Set command, CancellationToken cancellationToken = default)
    {
        TransientOperationScope.Require();
        Mutate(command.Key, 1);
        // The Local one decides the mode; the Replicated one then can't be honoured
        await commander.Call(new MixedModes_Local(command.Key), cancellationToken).ConfigureAwait(false);
        await commander.Call(new MixedModes_Replicated(command.Key), cancellationToken).ConfigureAwait(false);
    }

    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Local)]
    public virtual Task OnSetLocal(MixedModes_Local command, CancellationToken cancellationToken = default)
    {
        Invalidation.Defer(() => _ = Get(command.Key, default));
        return Task.CompletedTask;
    }

    [CommandHandler]
    [DeferredInvalidationMode(DeferredInvalidationMode.Replicated)]
    public virtual Task OnSetReplicated(MixedModes_Replicated command, CancellationToken cancellationToken = default)
    {
        Invalidation.Defer(() => _ = Count(default));
        return Task.CompletedTask;
    }
}
