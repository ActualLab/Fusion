using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Interception;
using ActualLab.Generators;
using ActualLab.Interception;
using ActualLab.Resilience;
using ActualLab.Versioning;

namespace ActualLab.CommandR.Internal;

public sealed class CommanderHub(ICommander commander, IServiceProvider services) : IHasServices
{
    public ICommander Commander { get; } = commander;
    public IServiceProvider Services { get; } = services;

    [field: AllowNull, MaybeNull]
    public HostId HostId => field ??= Services.GetRequiredService<HostId>();
    [field: AllowNull, MaybeNull]
    public UuidGenerator UuidGenerator => field ??= Services.GetRequiredService<UuidGenerator>();
    [field: AllowNull, MaybeNull]
    public VersionGenerator<long> VersionGenerator => field ??= Services.VersionGenerator<long>();
    [field: AllowNull, MaybeNull]
    public CommandHandlerResolver HandlerResolver => field ??= Services.GetRequiredService<CommandHandlerResolver>();
    [field: AllowNull, MaybeNull]
    public ChaosMaker ChaosMaker => field ??= Services.GetRequiredService<ChaosMaker>();
    [field: AllowNull, MaybeNull]
    public MomentClockSet Clocks => field ??= Services.Clocks();
    [field: AllowNull, MaybeNull]
    public CommandServiceInterceptor Interceptor
        => field ??= Services.GetRequiredService<CommandServiceInterceptor>();

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
