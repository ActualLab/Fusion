using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Tests.DbModel;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

public class EventCatcher(IServiceProvider services) : DbServiceBase<TestDbContext>(services), IComputeService
{
    public MutableState<ImmutableList<string>> Events { get; }
        = services.StateFactory().NewMutable(ImmutableList<string>.Empty);

    [CommandHandler]
    public virtual Task OnEvent(EventCatcher_Event command, CancellationToken cancellationToken = default)
    {
        if (Invalidation.IsActive)
            return Task.CompletedTask;

        Events.Set(command, static (command1, r) => r.Value.Add(command1.Id));
        return Task.CompletedTask;
    }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record EventCatcher_Event(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Id
) : ICommand<Unit>;
