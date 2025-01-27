using ActualLab.CommandR.Internal;
using ActualLab.Versioning;
using Cysharp.Text;

namespace ActualLab.CommandR.Operations;

public class Operation : IHasUuid, IHasId<Symbol>
{
    private readonly Lock _lock = LockFactory.Create();
    Symbol IHasId<Symbol>.Id => Uuid;

    public long? Index { get; set; }
    public Symbol Uuid { get; set; }
    public Symbol HostId { get; set; }
    public Moment LoggedAt { get; set; }
    public ICommand Command { get; set; }
    public MutablePropertyBag Items { get; set; }
    public ImmutableList<NestedOperation> NestedOperations { get; set; }
    public IOperationScope? Scope { get; set; }
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

    // ReSharper disable once ConvertToPrimaryConstructor
    public Operation(Symbol uuid,
        Symbol hostId,
        Moment loggedAt = default,
        ICommand? command = null,
        MutablePropertyBag? items = null,
        ImmutableList<NestedOperation>? nestedOperations = null,
        IOperationScope? scope = null)
    {
        Uuid = uuid;
        HostId = hostId;
        LoggedAt = loggedAt;
        Command = command!;
        Items = items ?? new();
        NestedOperations = nestedOperations ?? ImmutableList<NestedOperation>.Empty;
        Scope = scope;
    }

    public OperationEvent AddEvent(OperationEvent @event)
        => AddEvent(@event.Value!, @event.DelayUntil, @event.Uuid, @event.UuidConflictStrategy);
    public OperationEvent AddEvent(
        object value, Symbol uuid = default,
        KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        => AddEvent(value, default(Moment), uuid, uuidConflictStrategy);
    public OperationEvent AddEvent(
        object value, Moment delayUntil, Symbol uuid = default,
        KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
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

            var result = new OperationEvent(uuid, loggedAt, delayUntil, value, uuidConflictStrategy);
            Events = Events.Add(result);
            return result;
        }
    }

    public OperationEvent AddEvent(
        object value, TimeSpan delay, Symbol uuid = default,
        KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
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

            var result = new OperationEvent(uuid, loggedAt, delayUntil, value, uuidConflictStrategy);
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
