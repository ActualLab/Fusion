using ActualLab.CommandR.Internal;
using ActualLab.Generators;

namespace ActualLab.CommandR.Operations;

/// <summary>
/// Represents a recorded operation (a completed command execution) with its
/// invalidations, events, and metadata.
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

    public IOperationScope? Scope { get; set; }
    public long? Index { get; set; }
    public string Uuid { get; set; }
    public string HostId { get; set; }
    public Moment LoggedAt { get; set; }
    public ICommand Command { get; set; }
    public ImmutableList<ServiceCall> InvalidationCalls { get; private set; }
        = ImmutableList<ServiceCall>.Empty;
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
        IOperationScope? scope = null)
    {
        Uuid = uuid;
        HostId = hostId;
        LoggedAt = loggedAt;
        Command = command!;
        Scope = scope;
    }

    public OperationStoreMode? StoreMode {
        get => Scope.RequireActive().StoreMode;
        set => Scope.RequireActive().StoreMode = value;
    }

    // Add/RemoveInvalidationCall(s)

    // A call added here is applied after this operation commits, on every host that reads it.
    // Adding one also makes the operation worth storing - see DeferredInvalidationHelper's
    // GetDefaultStoreMode - which is what gets it to the other hosts in the first place.
    // Anything beyond invalidation belongs in an OperationCompletionHandler of your own.
    public Operation AddInvalidationCall(ServiceCall call)
    {
        lock (_lock)
            InvalidationCalls = InvalidationCalls.Add(call);
        return this;
    }

    public Operation AddInvalidationCalls(params ReadOnlySpan<ServiceCall> calls)
    {
        if (calls.Length == 0)
            return this;

        lock (_lock) {
            var builder = InvalidationCalls.ToBuilder();
            foreach (var call in calls)
                builder.Add(call);
            InvalidationCalls = builder.ToImmutable();
        }
        return this;
    }

    public Operation AddInvalidationCalls(IEnumerable<ServiceCall> calls)
    {
        lock (_lock)
            InvalidationCalls = InvalidationCalls.AddRange(calls);
        return this;
    }

    public bool RemoveInvalidationCall(ServiceCall call)
    {
        lock (_lock) {
            var oldCalls = InvalidationCalls;
            InvalidationCalls = oldCalls.Remove(call);
            return InvalidationCalls != oldCalls;
        }
    }

    public void RemoveInvalidationCalls()
    {
        lock (_lock)
            InvalidationCalls = ImmutableList<ServiceCall>.Empty;
    }

    public void RemoveInvalidationCalls(Func<ServiceCall, bool> predicate)
    {
        lock (_lock)
            InvalidationCalls = InvalidationCalls.RemoveAll(predicate.Invoke);
    }

    // Add/RemoveEvent(s)

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

    public void RemoveEvents()
    {
        var scope = Scope.RequireActive();
        if (scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock)
            Events = ImmutableList<OperationEvent>.Empty;
    }

    public void RemoveEvents(Func<OperationEvent, bool> predicate)
    {
        var scope = Scope.RequireActive();
        if (scope.IsTransient)
            throw Errors.TransientScopeOperationCannotHaveEvents();

        lock (_lock)
            Events = Events.RemoveAll(predicate.Invoke);
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
}
