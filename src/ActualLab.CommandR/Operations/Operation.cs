using Cysharp.Text;

namespace ActualLab.CommandR.Operations;

public class Operation(
    Symbol uuid,
    Symbol hostId,
    Moment startedAt,
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
    public Moment StartedAt { get; set; } = startedAt;
    public Moment LoggedAt { get; set; } = loggedAt;
    public ICommand Command { get; set; } = command!;
    public OptionSet Items { get; set; } = items ?? new();
    public List<NestedOperation> NestedOperations { get; set; } = nestedOperations ?? new();
    public IOperationScope? Scope { get; set; } = scope;

    public List<OperationEvent> Events => _events ??= new();
    public bool HasEvents => _events != null && _events.Count != 0;

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

    public void AddEvent(object @event, Symbol uuid = default)
        => Events.Add(OperationEvent.New(Scope!, @event, uuid));

    public ClosedDisposable<(Operation, List<NestedOperation>)> SuppressNestedOperationLogging()
    {
        var nestedCommands = NestedOperations;
        NestedOperations = new();
        return Disposable.NewClosed(
            (Operation: this, OldNestedCommands: nestedCommands),
            state => state.Operation.NestedOperations = state.OldNestedCommands);
    }
}
