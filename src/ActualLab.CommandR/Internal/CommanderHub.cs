using ActualLab.CommandR.Interception;
using ActualLab.Interception;
using ActualLab.Resilience;
using ActualLab.Versioning;

namespace ActualLab.CommandR.Internal;

public sealed class CommanderHub(ICommander commander, IServiceProvider services) : IHasServices
{
    public ICommander Commander { get; } = commander;
    public IServiceProvider Services { get; } = services;

    public HostId HostId => field ??= Services.GetRequiredService<HostId>();
    public VersionGenerator<long> VersionGenerator => field ??= Services.VersionGenerator<long>();
    public CommandHandlerResolver HandlerResolver => field ??= Services.GetRequiredService<CommandHandlerResolver>();
    public ChaosMaker ChaosMaker => field ??= Services.GetRequiredService<ChaosMaker>();
    public MomentClockSet Clocks => field ??= Services.Clocks();
    public CommandServiceInterceptor Interceptor => field ??= Services.GetRequiredService<CommandServiceInterceptor>();

    public IProxy NewProxy(
        IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type implementationType,
        bool initialize = true)
    {
        var interceptor = Interceptor;
        interceptor.ValidateType(implementationType);
        return services.ActivateProxy(implementationType, interceptor, initialize);
    }
}
