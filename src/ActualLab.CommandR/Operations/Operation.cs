using ActualLab.CommandR.Internal;
using Cysharp.Text;

namespace ActualLab.CommandR.Operations;

public class Operation(
    Symbol uuid,
    Symbol hostId,
    Moment loggedAt = default,
    ICommand? command = null,
    OptionSet? items = null,
    List<NestedOperation>? nestedOperations = null,
    IOperationScope? scope = null
    ) : IHasUuid, IHasId<Symbol>
{
    private List<OperationEvent>? _events;
    Symbol IHasId<Symbol>.Id => Uuid;

    public long? Index { get; set; }
    public Symbol Uuid { get; set; } = uuid;
    public Symbol HostId { get; set; } = hostId;
    public Moment LoggedAt { get; set; } = loggedAt;
    public ICommand Command { get; set; } = command!;
    public OptionSet Items { get; set; } = items ?? new();
    public List<NestedOperation> NestedOperations { get; set; } = nestedOperations ?? new();
    public IOperationScope? Scope { get; set; } = scope;

    public IReadOnlyList<OperationEvent> Events
        => (IReadOnlyList<OperationEvent>?)_events ?? Array.Empty<OperationEvent>();

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

        var commanderHub = Scope.CommandContext.Commander.Hub;
        if (uuid.IsEmpty)
            uuid = commanderHub.UuidGenerator.Next();
        var loggedAt = commanderHub.Clocks.SystemClock.Now;
        if (delayUntil == default)
            delayUntil = value is IHasDelayUntil hasDelayUntil ? hasDelayUntil.DelayUntil : loggedAt;
        if (delayUntil < loggedAt)
            delayUntil = loggedAt;

        var result = new OperationEvent(uuid, loggedAt, delayUntil, value);
        (_events ??= new()).Add(result);
        return result;
    }

    public OperationEvent AddEvent(object value, TimeSpan delay, Symbol uuid = default)
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        var commanderHub = Scope.CommandContext.Commander.Hub;
        if (uuid.IsEmpty)
            uuid = commanderHub.UuidGenerator.Next();
        var loggedAt = commanderHub.Clocks.SystemClock.Now;
        var delayUntil = loggedAt + delay.Positive();

        var result = new OperationEvent(uuid, loggedAt, delayUntil, value);
        (_events ??= new()).Add(result);
        return result;
    }

    public bool RemoveEvent(OperationEvent operationEvent)
        => RemoveEvent(operationEvent.Uuid);
    public bool RemoveEvent(Symbol uuid)
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        return _events?.RemoveAll(x => x.Uuid == uuid) > 0;
    }

    public void ClearEvents()
    {
        if (Scope is not { IsUsed: true, IsCommitted: null })
            throw Errors.ActiveOperationRequired();
        if (Scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        _events = null;
    }

    public ClosedDisposable<(Operation, List<NestedOperation>)> SuppressNestedOperationLogging()
    {
        var nestedCommands = NestedOperations;
        NestedOperations = new();
        return Disposable.NewClosed(
            (Operation: this, OldNestedCommands: nestedCommands),
            state => state.Operation.NestedOperations = state.OldNestedCommands);
    }
}
