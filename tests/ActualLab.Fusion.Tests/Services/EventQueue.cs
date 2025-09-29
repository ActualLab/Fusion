using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Tests.Model;
using ActualLab.Generators;
using MessagePack;

namespace ActualLab.Fusion.Tests.Services;

public class EventQueue(IServiceProvider services, ITestOutputHelper output)
    : DbServiceBase<TestDbContext>(services), IComputeService
{
    [CommandHandler]
    public virtual async Task Add(EventQueue_Add command, CancellationToken cancellationToken = default)
    {
        var events = command.Events;
        if (events.Length == 0)
            return;

        if (Invalidation.IsActive)
            return;

        var context = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _ = context.ConfigureAwait(false);

        var operation = CommandContext.GetCurrent().Operation;
        var mustCreateOperation = RandomShared.NextDouble() < 0.5;
        if (!mustCreateOperation) {
            output.WriteLine($"MustCreateOperation: {mustCreateOperation}");
            operation.MustCreate(mustCreateOperation);
        }
        foreach (var @event in events)
            operation.AddEvent(@event);
    }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record EventQueue_Add(
    [property: DataMember, MemoryPackOrder(0), Key(0)] params OperationEvent[] Events
) : ICommand<Unit>;
