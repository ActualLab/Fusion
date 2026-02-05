using ActualLab.CommandR.Internal;
using ActualLab.Generators;

namespace ActualLab.CommandR.Operations;

/// <summary>
/// Represents a recorded operation (a completed command execution) with its
/// nested operations, events, and metadata.
/// </summary>
public class Operation : IHasUuid, IHasId<string>
{
    public static UuidGenerator UuidGenerator { get; set; } = UlidUuidGenerator.Instance;

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
            uuid = UuidGenerator.Next();
        return new Operation(uuid, hostId, clock.Now, scope: scope);
    }

    public static Operation NewTransient(IOperationScope scope)
        => New(scope, $"{UuidGenerator.Next()}-local");

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

    public void MustStore(bool mustStore)
        => Scope.RequireActive().MustStoreOperation = mustStore;

    // Add/Remove/ClearEvents

    public OperationEvent AddEvent(object? value)
        => AddEvent(new OperationEvent(value));
    public OperationEvent AddEvent(string uuid, object? value)
        => AddEvent(new OperationEvent(uuid, value));

    public OperationEvent AddEvent(IOperationEventSource operationEventSource)
    {
        var scope = Scope.RequireActive();
        if (scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        var @event = operationEventSource.ToOperationEvent(scope.CommandContext.Services);
        @event.LoggedAt = scope.CommandContext.Commander.Hub.Clocks.SystemClock.Now;
        lock (_lock)
            Events = Events.Add(@event);
        return @event;
    }

    public OperationEvent AddEvent(OperationEvent @event)
    {
        var scope = Scope.RequireActive();
        if (scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        @event.LoggedAt = scope.CommandContext.Commander.Hub.Clocks.SystemClock.Now;
        lock (_lock)
            Events = Events.Add(@event);
        return @event;
    }

    public bool RemoveEvent(OperationEvent @event)
        => RemoveEvent(@event.Uuid);
    public bool RemoveEvent(string uuid)
    {
        var scope = Scope.RequireActive();
        if (scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock) {
            var oldEvents = Events;
            Events = oldEvents.RemoveAll(x => string.Equals(x.Uuid, uuid, StringComparison.Ordinal));
            return Events != oldEvents;
        }
    }

    public void ClearEvents()
    {
        var scope = Scope.RequireActive();
        if (scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock)
            Events = ImmutableList<OperationEvent>.Empty;
    }

    // Add/RemoveCompletionHandler

    public void AddCompletionHandler(Func<IOperationScope, Task> handler)
    {
        var scope = Scope.RequireActive();
        scope.CompletionHandlers = scope.CompletionHandlers.Add(handler);
    }

    public void RemoveCompletionHandler(Func<IOperationScope, Task> handler)
    {
        var scope = Scope.RequireActive();
        scope.CompletionHandlers = scope.CompletionHandlers.Remove(handler);
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
