using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Tests.Model;

namespace ActualLab.Fusion.Tests.Services;

public class EventQueue(IServiceProvider services) : DbServiceBase<TestDbContext>(services), IComputeService
{
    [CommandHandler]
    public virtual async Task Add(EventQueue_Add command, CancellationToken cancellationToken = default)
    {
        var events = command.Events;
        if (events.Length == 0)
            return;

        if (Invalidation.IsActive)
            return;

        var context = await DbHub.CreateCommandDbContext(cancellationToken).ConfigureAwait(false);
        await using var _ = context.ConfigureAwait(false);

        var operation = CommandContext.GetCurrent().Operation;
        foreach (var @event in events)
            operation.AddEvent(@event);
    }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public partial record EventQueue_Add(
    [property: DataMember, MemoryPackOrder(0)] params OperationEvent[] Events
) : ICommand<Unit>;
