namespace ActualLab.CommandR.Operations;

public class Operation(
    Symbol id,
    Symbol hostId,
    Moment startTime,
    Moment commitTime = default,
    ICommand? command = null,
    OptionSet? items = null,
    List<NestedOperation>? nestedOperations = null,
    IOperationScope? scope = null
    ) : IRequirementTarget
{
    private static long _lastTransientId;

    public long? Index { get; set; }
    public Symbol Id { get; set; } = id;
    public Symbol HostId { get; set; } = hostId;
    public Moment StartTime { get; set; } = startTime;
    public Moment CommitTime { get; set; } = commitTime;
    public ICommand Command { get; set; } = command!;
    public OptionSet Items { get; set; } = items ?? new();
    public List<NestedOperation> NestedOperations { get; set; } = nestedOperations ?? new();
    public IOperationScope? Scope { get; set; } = scope;

    public static Operation New(IOperationScope scope, Symbol id = default)
    {
        var context = scope.CommandContext;
        var commander = context.Commander;
        var clock = commander.Clocks.SystemClock;
        var hostId = commander.HostId;
        if (id.IsEmpty)
            id = Ulid.NewUlid().ToString();
        return new Operation(id, hostId, clock.Now, scope: scope);
    }

    public static Operation NewTransient(IOperationScope scope)
        => New(scope, $"Local-{Interlocked.Increment(ref _lastTransientId)}");

    public Operation()
        : this(default, default, default)
    { }

    public ClosedDisposable<(Operation, List<NestedOperation>)> SuppressNestedOperationLogging()
    {
        var nestedCommands = NestedOperations;
        NestedOperations = new();
        return Disposable.NewClosed(
            (Operation: this, OldNestedCommands: nestedCommands),
            state => state.Operation.NestedOperations = state.OldNestedCommands);
    }
}
