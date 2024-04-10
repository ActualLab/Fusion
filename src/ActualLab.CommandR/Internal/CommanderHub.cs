using ActualLab.Generators;
using ActualLab.Resilience;

namespace ActualLab.CommandR.Internal;

public sealed class CommanderHub(ICommander commander, IServiceProvider services) : IHasServices
{
    private HostId? _hostId;
    private UuidGenerator? _uuidGenerator;
    private CommandHandlerResolver? _handlerResolver;
    private ChaosMaker? _chaosMaker;
    private MomentClockSet? _clocks;

    public ICommander Commander { get; } = commander;
    public IServiceProvider Services { get; } = services;
    public HostId HostId => _hostId ??= Services.GetRequiredService<HostId>();
    public UuidGenerator UuidGenerator => _uuidGenerator ??= Services.GetRequiredService<UuidGenerator>();
    public CommandHandlerResolver HandlerResolver => _handlerResolver ??= Services.GetRequiredService<CommandHandlerResolver>();
    public ChaosMaker ChaosMaker => _chaosMaker ??= Services.GetRequiredService<ChaosMaker>();
    public MomentClockSet Clocks => _clocks ??= Services.Clocks();
}
