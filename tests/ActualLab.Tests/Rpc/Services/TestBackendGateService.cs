using ActualLab.Rpc;
using MessagePack;

namespace ActualLab.Tests.Rpc;

// BackendGateCommand is abstract and isn't [RpcSerializable], so RPC wraps its arguments
// polymorphically: the wire carries the derived type, which is what lets a client deliver
// a BackendGate_DerivedBackend to a method whose declared parameter isn't backend-only.
public abstract record BackendGateCommand : ICommand<string>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public sealed partial record BackendGate_Public(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Name
) : BackendGateCommand;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public sealed partial record BackendGate_DerivedBackend(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Name
) : BackendGateCommand, IBackendCommand<string>;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public sealed partial record BackendGate_Backend(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Name
) : IBackendCommand<string>;

public interface ITestBackendGateService : IRpcService
{
    public Task<string> OnAny(BackendGateCommand command, CancellationToken cancellationToken = default);
    public Task<string> OnBackend(BackendGate_Backend command, CancellationToken cancellationToken = default);
}

public class TestBackendGateService : ITestBackendGateService
{
    // Both methods are unreachable: RpcInboundCommandHandler replaces the direct invocation
    // of a command-shaped method with an ICommander call.
    public virtual Task<string> OnAny(BackendGateCommand command, CancellationToken cancellationToken = default)
        => Task.FromResult("direct");
    public virtual Task<string> OnBackend(BackendGate_Backend command, CancellationToken cancellationToken = default)
        => Task.FromResult("direct");
}

public class TestBackendGateHandlers : ICommandService
{
    [CommandHandler]
    public virtual Task<string> OnPublic(BackendGate_Public command, CancellationToken cancellationToken = default)
        => Task.FromResult("public:" + command.Name);

    [CommandHandler]
    public virtual Task<string> OnDerivedBackend(
        BackendGate_DerivedBackend command, CancellationToken cancellationToken = default)
        => Task.FromResult("backend:" + command.Name);

    [CommandHandler]
    public virtual Task<string> OnBackend(BackendGate_Backend command, CancellationToken cancellationToken = default)
        => Task.FromResult("backend:" + command.Name);
}
