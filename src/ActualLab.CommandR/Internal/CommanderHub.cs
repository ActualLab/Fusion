using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Interception;
using ActualLab.Generators;
using ActualLab.Interception;
using ActualLab.Resilience;
using ActualLab.Versioning;

namespace ActualLab.CommandR.Internal;

public sealed class CommanderHub(ICommander commander, IServiceProvider services) : IHasServices
{
    private HostId? _hostId;
    private UuidGenerator? _uuidGenerator;
    private VersionGenerator<long>? _versionGenerator;
    private CommandHandlerResolver? _handlerResolver;
    private ChaosMaker? _chaosMaker;
    private MomentClockSet? _clocks;
    private CommandServiceInterceptor? _interceptor;

    public ICommander Commander { get; } = commander;
    public IServiceProvider Services { get; } = services;
    public HostId HostId => _hostId ??= Services.GetRequiredService<HostId>();
    public UuidGenerator UuidGenerator => _uuidGenerator ??= Services.GetRequiredService<UuidGenerator>();
    public VersionGenerator<long> VersionGenerator => _versionGenerator ??= Services.VersionGenerator<long>();
    public CommandHandlerResolver HandlerResolver => _handlerResolver ??= Services.GetRequiredService<CommandHandlerResolver>();
    public ChaosMaker ChaosMaker => _chaosMaker ??= Services.GetRequiredService<ChaosMaker>();
    public MomentClockSet Clocks => _clocks ??= Services.Clocks();

    public CommandServiceInterceptor Interceptor
        => _interceptor ??= Services.GetRequiredService<CommandServiceInterceptor>();

    public IProxy NewProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        bool initialize = true)
    {
        var interceptor = Interceptor;
        interceptor.ValidateType(implementationType);
        return services.ActivateProxy(implementationType, interceptor, null, initialize);
    }
}
