using ActualLab.CommandR.Internal;
using ActualLab.Versioning;
using Cysharp.Text;

namespace ActualLab.CommandR.Operations;

public class Operation : IHasUuid, IHasId<string>
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    string IHasId<string>.Id => Uuid;

    public long? Index { get; set; }
    public string Uuid { get; set; }
    public string HostId { get; set; }
    public Moment LoggedAt { get; set; }
    public ICommand Command { get; set; }
    public MutablePropertyBag Items { get; set; }
    public ImmutableList<NestedOperation> NestedOperations { get; set; }
    public IOperationScope? Scope { get; set; }
    public ImmutableList<OperationEvent> Events { get; private set; }
        = ImmutableList<OperationEvent>.Empty;

    public static Operation New(IOperationScope scope, string uuid = "")
    {
        var commanderHub = scope.CommandContext.Commander.Hub;
        var clock = commanderHub.Clocks.SystemClock;
        var hostId = commanderHub.HostId;
        if (uuid.IsNullOrEmpty())
            uuid = commanderHub.UuidGenerator.Next();
        return new Operation(uuid, hostId, clock.Now, scope: scope);
    }

    public static Operation NewTransient(IOperationScope scope)
        => New(scope, ZString.Concat(Ulid.NewUlid(), "-local"));

    public Operation()
        : this("", "")
    { }

    // ReSharper disable once ConvertToPrimaryConstructor
    public Operation(
        string uuid,
        string hostId,
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
        object value, string uuid = "",
        KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
        => AddEvent(value, default(Moment), uuid, uuidConflictStrategy);
    public OperationEvent AddEvent(
        object value, Moment delayUntil, string uuid = "",
        KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock) {
            var commanderHub = Scope.CommandContext.Commander.Hub;
            if (uuid.IsNullOrEmpty())
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
        object value, TimeSpan delay, string uuid = "",
        KeyConflictStrategy uuidConflictStrategy = KeyConflictStrategy.Fail)
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock) {
            var commanderHub = Scope.CommandContext.Commander.Hub;
            if (uuid.IsNullOrEmpty())
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
    public bool RemoveEvent(string uuid)
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock) {
            var oldEvents = Events;
            Events = oldEvents.RemoveAll(x => string.Equals(x.Uuid, uuid, StringComparison.Ordinal));
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
