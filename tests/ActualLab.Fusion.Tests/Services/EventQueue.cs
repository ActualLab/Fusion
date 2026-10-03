using ActualLab.CommandR.Operations;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Tests.DbModel;
using ActualLab.Generators;
using ActualLab.Versioning;
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

        var context = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _ = context.ConfigureAwait(false);

        var operation = CommandContext.GetCurrent().Operation;
        var mustStoreOperation = RandomShared.NextDouble() < 0.5;
        if (!mustStoreOperation) {
            output.WriteLine($"StoreMode: {OperationStoreMode.None}");
            operation.StoreMode = OperationStoreMode.None;
        }
        foreach (var item in events)
            operation.AddEvent(item.ToEvent());
    }

    [CommandHandler]
    public virtual async Task AddThenRemove(
        EventQueue_AddThenRemove command, CancellationToken cancellationToken = default)
    {
        var context = await DbHub.CreateOperationDbContext(cancellationToken).ConfigureAwait(false);
        await using var _ = context.ConfigureAwait(false);

        var operation = CommandContext.GetCurrent().Operation;
        var added = command.Events.Select(x => operation.AddEvent(x.ToEvent())).ToList();
        if (command.RemoveAll) {
            operation.RemoveEvents();
            return;
        }

        var removeUuids = command.RemoveUuids;
        if (removeUuids.Length == 0)
            return;

        // The first one goes by its OperationEvent and the rest by predicate, so both overloads
        // are exercised rather than just the one a caller happens to reach for
        operation.RemoveEvent(added.Single(x => string.Equals(x.Uuid, removeUuids[0], StringComparison.Ordinal)));
        if (removeUuids.Length > 1) {
            var rest = removeUuids.Skip(1).ToHashSet(StringComparer.Ordinal);
            operation.RemoveEvents(x => rest.Contains(x.Uuid));
        }
    }
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record EventQueue_AddThenRemove(
    [property: DataMember, MemoryPackOrder(0), Key(0)] EventQueue_Item[] Events,
    [property: DataMember, MemoryPackOrder(1), Key(1)] string[] RemoveUuids,
    [property: DataMember, MemoryPackOrder(2), Key(2)] bool RemoveAll = false
) : ICommand<Unit>;

// An OperationEvent isn't a serializable type - the DB stores it decomposed into DbEvent columns,
// never as an object - so a command can't carry one. This is what an event is made of instead.
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record EventQueue_Item(
    [property: DataMember, MemoryPackOrder(0), Key(0)] string Uuid,
    [property: DataMember, MemoryPackOrder(1), Key(1)] EventCatcher_Event Value,
    [property: DataMember, MemoryPackOrder(2), Key(2)] Moment DelayUntil,
    [property: DataMember, MemoryPackOrder(3), MemoryPackAllowSerialize, Key(3)] KeyConflictStrategy UuidConflictStrategy
) {
    public OperationEvent ToEvent()
        => new(Uuid, Value) {
            DelayUntil = DelayUntil,
            UuidConflictStrategy = UuidConflictStrategy,
        };
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public partial record EventQueue_Add(
    [property: DataMember, MemoryPackOrder(0), Key(0)] params EventQueue_Item[] Events
) : ICommand<Unit>;
