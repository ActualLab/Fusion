using ActualLab.CommandR.Internal;
using Cysharp.Text;

namespace ActualLab.CommandR.Operations;

public class Operation(
    Symbol uuid,
    Symbol hostId,
    Moment loggedAt = default,
    ICommand? command = null,
    MutablePropertyBag? items = null,
    ImmutableList<NestedOperation>? nestedOperations = null,
    IOperationScope? scope = null
    ) : IHasUuid, IHasId<Symbol>
{
    private readonly object _lock = new();
    Symbol IHasId<Symbol>.Id => Uuid;

    public long? Index { get; set; }
    public Symbol Uuid { get; set; } = uuid;
    public Symbol HostId { get; set; } = hostId;
    public Moment LoggedAt { get; set; } = loggedAt;
    public ICommand Command { get; set; } = command!;
    public MutablePropertyBag Items { get; set; } = items ?? new();
    public ImmutableList<NestedOperation> NestedOperations { get; set; }
        = nestedOperations ?? ImmutableList<NestedOperation>.Empty;
    public IOperationScope? Scope { get; set; } = scope;
    public ImmutableList<OperationEvent> Events { get; private set; }
        = ImmutableList<OperationEvent>.Empty;

    public static Operation New(IOperationScope scope, Symbol uuid = default)
    {
        var commanderHub = scope.CommandContext.Commander.Hub;
        var clock = commanderHub.Clocks.SystemClock;
        var hostId = commanderHub.HostId;
        if (uuid.IsEmpty)
            uuid = commanderHub.UuidGenerator.Next();
        return new Operation(uuid, hostId, clock.Now, scope: scope);
    }

    public static Operation NewTransient(IOperationScope scope)
        => New(scope, ZString.Concat(Ulid.NewUlid(), "-local"));

    public Operation()
        : this(default, default, default)
    { }

    public OperationEvent AddEvent(object value, Symbol uuid = default)
        => AddEvent(value, default(Moment), uuid);

    public OperationEvent AddEvent(object value, Moment delayUntil, Symbol uuid = default)
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock) {
            var commanderHub = Scope.CommandContext.Commander.Hub;
            if (uuid.IsEmpty)
                uuid = commanderHub.UuidGenerator.Next();
            var loggedAt = commanderHub.Clocks.SystemClock.Now;
            if (delayUntil == default)
                delayUntil = value is IHasDelayUntil hasDelayUntil ? hasDelayUntil.DelayUntil : loggedAt;
            if (delayUntil < loggedAt)
                delayUntil = loggedAt;

            var result = new OperationEvent(uuid, loggedAt, delayUntil, value);
            Events = Events.Add(result);
            return result;
        }
    }

    public OperationEvent AddEvent(object value, TimeSpan delay, Symbol uuid = default)
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock) {
            var commanderHub = Scope.CommandContext.Commander.Hub;
            if (uuid.IsEmpty)
                uuid = commanderHub.UuidGenerator.Next();
            var loggedAt = commanderHub.Clocks.SystemClock.Now;
            var delayUntil = loggedAt + delay.Positive();

            var result = new OperationEvent(uuid, loggedAt, delayUntil, value);
            Events = Events.Add(result);
            return result;
        }
    }

    public bool RemoveEvent(OperationEvent operationEvent)
        => RemoveEvent(operationEvent.Uuid);
    public bool RemoveEvent(Symbol uuid)
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock) {
            var oldEvents = Events;
            Events = oldEvents.RemoveAll(x => x.Uuid == uuid);
            return Events != oldEvents;
        }
    }

    public void ClearEvents()
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock)
            Events = ImmutableList<OperationEvent>.Empty;
    }

    public ClosedDisposable<(Operation, ImmutableList<NestedOperation>)> SuppressNestedOperationLogging()
    {
        var nestedCommands = NestedOperations;
        NestedOperations = ImmutableList<NestedOperation>.Empty;
        return Disposable.NewClosed(
            (Operation: this, OldNestedCommands: nestedCommands),
            state => state.Operation.NestedOperations = state.OldNestedCommands);
    }
}
