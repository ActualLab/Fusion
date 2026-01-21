# Advanced Compute Patterns

This document covers advanced patterns for working with Fusion's compute infrastructure, including pseudo-methods
for batch invalidation, accessing the current computed value, and observing changes.


## Pseudo-Methods for Batch Invalidation

::: tip Use Sparingly
Pseudo-methods are a niche pattern for specific scenarios. Most applications don't need them.
Only consider this pattern when you have methods with many parameter variations that all need
invalidation together.
:::

When a compute method has multiple parameters (like pagination limits), invalidating all cached results
can be challenging. The **pseudo-method pattern** solves this by creating a single dependency that all
related queries share.

### The Problem

Consider a `ListIds` method with a `limit` parameter:

<!-- snippet: PartFPatterns_Problem -->
```cs
[ComputeMethod]
public virtual async Task<Ulid[]> ListIds(string folder, int limit, CancellationToken ct = default)
{
    // Returns up to `limit` IDs from the folder
    return Array.Empty<Ulid>();
}
```
<!-- endSnippet -->

When a new item is added, you need to invalidate `ListIds(folder, 10)`, `ListIds(folder, 50)`, etc.
But you don't know which specific limits have been called.

### The Solution: Pseudo-Methods

Create a "pseudo" compute method that acts as a shared dependency:

<!-- snippet: PartFPatterns_PseudoMethod -->
```cs
// Pseudo-method: returns immediately, exists only to create a dependency
[ComputeMethod]
protected virtual Task<Unit> PseudoListIds(string folder)
    => TaskExt.UnitTask;

[ComputeMethod]
public virtual async Task<Ulid[]> ListIds(string folder, int limit, CancellationToken ct = default)
{
    // Create dependency on the pseudo-method
    await PseudoListIds(folder);

    // Actual implementation
    return await FetchIds(folder, limit, ct);
}
```
<!-- endSnippet -->

Now, when invalidating:

<!-- snippet: PartFPatterns_InvalidatePseudo -->
```cs
[CommandHandler]
public virtual async Task AddItem(AddItemCommand command, CancellationToken ct = default)
{
    var folder = command.Folder;
    if (Invalidation.IsActive) {
        // This invalidates ALL ListIds(folder, <any_limit>) calls
        _ = PseudoListIds(folder);
        return;
    }

    // Actual implementation
    await AddItemToDb(command, ct);
}
```
<!-- endSnippet -->

### Naming Convention

Prefix pseudo-methods with "Pseudo":
- `PseudoListIds(string folder)`
- `PseudoGetAnyTail()`
- `PseudoListSymbols()`

### Hierarchical Dependencies

Pseudo-methods can call themselves recursively to create tree-like dependency structures. This is useful
when you have hierarchical data (spatial indices, organizational trees, etc.) and want to invalidate
at different granularities.

<!-- snippet: PartFPatterns_HierarchicalBinaryTree -->
```cs
// Binary tree style: each level depends on its parent
[ComputeMethod]
protected virtual async Task<Unit> PseudoRegion(int level, int index)
{
    if (level > 0) {
        // Create dependency on parent level
        await PseudoRegion(level - 1, index / 2);
    }
    return default;
}

// Octree style for 3D spatial data
[ComputeMethod]
protected virtual async Task<Unit> PseudoOctant(int level, int x, int y, int z)
{
    if (level > 0) {
        // Create dependency on parent octant
        await PseudoOctant(level - 1, x / 2, y / 2, z / 2);
    }
    return default;
}
```
<!-- endSnippet -->

With this pattern:
- Invalidating a leaf node only affects queries depending on that specific node
- Invalidating a parent node cascades to all children (via Fusion's dependency tracking)
- You can invalidate at any level of the hierarchy

<!-- snippet: PartFPatterns_HierarchicalInvalidation -->
```cs
// Invalidate just one leaf region
using (Invalidation.Begin())
    _ = PseudoRegion(3, 5);  // Only queries for region (3,5) and its ancestors

// Invalidate an entire subtree by invalidating its root
using (Invalidation.Begin())
    _ = PseudoRegion(1, 0);  // All regions under (1,0) get invalidated
```
<!-- endSnippet -->

### Complete Example

<!-- snippet: PartFPatterns_CompleteTodoService -->
```cs
public class TodoService : IComputeService
{
    // Pseudo-method for batch invalidation
    [ComputeMethod]
    protected virtual Task<Unit> PseudoListIds(Session session)
        => TaskExt.UnitTask;

    [ComputeMethod]
    public virtual async Task<Ulid[]> ListIds(Session session, int count, CancellationToken ct = default)
    {
        // Establish dependency on pseudo-method
        await PseudoListIds(session);

        // Actual query
        return await QueryIds(session, count, ct);
    }

    [CommandHandler]
    public virtual async Task<TodoItem> AddOrUpdate(AddTodoCommand command, CancellationToken ct = default)
    {
        var session = command.Session;
        if (Invalidation.IsActive) {
            _ = Get(session, command.Todo.Id, default);
            // Invalidate all ListIds variants for this session
            _ = PseudoListIds(session);
            _ = GetSummary(session, default);
            return null!;
        }

        // Actual implementation
        return await SaveTodo(command, ct);
    }

    [ComputeMethod]
    public virtual Task<TodoItem?> Get(Session session, Ulid id, CancellationToken ct)
        => Task.FromResult<TodoItem?>(null);

    [ComputeMethod]
    public virtual Task<string> GetSummary(Session session, CancellationToken ct)
        => Task.FromResult("");

    private Task<Ulid[]> QueryIds(Session session, int count, CancellationToken ct)
        => Task.FromResult(Array.Empty<Ulid>());

    private Task<TodoItem> SaveTodo(AddTodoCommand command, CancellationToken ct)
        => Task.FromResult(command.Todo);
}
```
<!-- endSnippet -->


## Computed.GetCurrent()

Inside a compute method, you can access the `Computed<T>` being created using `Computed.GetCurrent()`.
This is useful for:

- Scheduling automatic invalidation
- Attaching invalidation callbacks
- Accessing computed metadata

### Scheduled Invalidation

<!-- snippet: PartFPatterns_ScheduledInvalidation -->
```cs
[ComputeMethod]
public virtual async Task<DateTime> GetServerTime(CancellationToken ct = default)
{
    // Invalidate this computed after 1 second
    Computed.GetCurrent().Invalidate(TimeSpan.FromSeconds(1));
    return DateTime.UtcNow;
}
```
<!-- endSnippet -->

### Invalidation Callbacks

<!-- snippet: PartFPatterns_InvalidationCallbacks -->
```cs
[ComputeMethod]
public virtual async Task<Resource> GetResource(string key, CancellationToken ct = default)
{
    var computed = Computed.GetCurrent();

    // Register cleanup when this computed is invalidated
    computed.Invalidated += c => {
        Log.LogDebug("Resource {Key} computed was invalidated", key);
        // Release resources, cancel subscriptions, etc.
    };

    return await LoadResource(key, ct);
}
```
<!-- endSnippet -->

### Conditional Invalidation Delay

<!-- snippet: PartFPatterns_ConditionalInvalidationDelay -->
```cs
[ComputeMethod]
public virtual async Task<PeerState> GetPeerState(RpcPeerStub peer, CancellationToken ct = default)
{
    var computed = Computed.GetCurrent();
    var state = await peer.GetState(ct);

    // Different invalidation delays based on state
    var delay = state.IsConnected
        ? TimeSpan.FromSeconds(10)   // Poll less frequently when connected
        : TimeSpan.FromSeconds(1);   // Poll more frequently when disconnected

    computed.Invalidate(delay);
    return state;
}
```
<!-- endSnippet -->


## Computed.Changes() Observable

The `Changes()` extension method creates an `IAsyncEnumerable<Computed<T>>` that yields:
1. The current computed value (immediately)
2. Each new computed value as changes occur

### Basic Usage

<!-- snippet: PartFPatterns_ChangesBasic -->
```cs
var computed = await Computed.Capture(() => example.GetData());

await foreach (var c in computed.Changes(cancellationToken)) {
    Console.WriteLine($"Value: {c.Value}");
}
```
<!-- endSnippet -->

### With Update Delayer

Control how quickly updates are processed:

<!-- snippet: PartFPatterns_ChangesWithDelayer -->
```cs
// Wait 1 second between updates
await foreach (var c in computed.Changes(FixedDelayer.Get(1), ct)) {
    ProcessValue(c.Value);
}
```
<!-- endSnippet -->

### Deconstruction Pattern

`Computed<T>` supports deconstruction to access `ValueOrDefault` and `Error` without throwing:

<!-- snippet: PartFPatterns_ChangesDeconstruction -->
```cs
await foreach (var (value, error) in computed.Changes(ct)) {
    if (error != null)
        HandleError(error);
    else
        DisplayValue(value);
}
```
<!-- endSnippet -->

### Creating RpcStream from Changes

Use `Changes()` to create real-time streams:

<!-- snippet: PartFPatterns_ChangesToRpcStream -->
```cs
public async Task<RpcStream<StockPrice>> WatchPrice(string symbol, CancellationToken ct = default)
{
    var computed = await Computed.Capture(() => GetPrice(symbol));

    var stream = computed.Changes(FixedDelayer.Get(0.5), ct)
        .Select(c => c.Value);

    return RpcStream.New(stream);
}
```
<!-- endSnippet -->


## Computed.When() Predicate

Wait for a computed value to satisfy a condition:

<!-- snippet: PartFPatterns_WhenBasic -->
```cs
var computed = await Computed.Capture(() => counter.Get("a"));

// Wait until value reaches 10
computed = await computed.When(x => x >= 10, cancellationToken);
Console.WriteLine($"Reached: {computed.Value}");
```
<!-- endSnippet -->

With update delayer:

<!-- snippet: PartFPatterns_WhenWithDelayer -->
```cs
// Check every second
computed = await computed.When(
    x => x.Status == "Complete",
    FixedDelayer.Get(1),
    cancellationToken);
```
<!-- endSnippet -->


## Combining Patterns

### Pseudo-Methods with Cleanup Callbacks

<!-- snippet: PartFPatterns_CombinedPseudoAndCallbacks -->
```cs
public class SubscriptionService : IComputeService
{
    private readonly ConcurrentDictionary<string, IDisposable> _subscriptions = new();
    private readonly MessageBroker _broker = new();

    [ComputeMethod]
    protected virtual Task<Unit> PseudoWatchTopic(string topic)
        => TaskExt.UnitTask;

    [ComputeMethod]
    public virtual async Task<Message[]> GetMessages(string topic, int count, CancellationToken ct = default)
    {
        await PseudoWatchTopic(topic);

        var computed = Computed.GetCurrent();

        // Setup external subscription on first computation
        _subscriptions.GetOrAdd(topic, t => {
            var sub = _broker.Subscribe(t, () => {
                using var __ = Invalidation.Begin();
                _ = PseudoWatchTopic(t); // Invalidate when broker notifies
            });

            computed.Invalidated += _ => {
                // Don't clean up immediately - another computed might need it
            };

            return sub;
        });

        return await QueryMessages(topic, count, ct);
    }

    private Task<Message[]> QueryMessages(string topic, int count, CancellationToken ct)
        => Task.FromResult(Array.Empty<Message>());
}
```
<!-- endSnippet -->

### Changes() with Error Handling

<!-- snippet: PartFPatterns_ChangesErrorHandling -->
```cs
static async Task MonitorService(IServiceWithStatus service, ILogger _logger, CancellationToken ct)
{
    var computed = await Computed.Capture(() => service.GetStatus());

    await foreach (var c in computed.Changes(FixedDelayer.Get(5), ct)) {
        var (status, error) = c;

        if (error != null) {
            _logger.LogError(error, "Status check failed");
            // Continue watching - transient errors will retry automatically
            continue;
        }

        if (status.HasAlert)
            await NotifyOperators(status);
    }
}

static Task NotifyOperators(ServiceStatus status) => Task.CompletedTask;
```
<!-- endSnippet -->


## Best Practices

1. **Pseudo-methods are rare**: Most apps don't need them. Only use when you truly have unbounded parameter variations
2. **Keep pseudo-methods protected**: They're implementation details, not part of the public API
3. **Dispose Changes() properly**: Use `CancellationToken` to stop the async enumerable
4. **Avoid long-running callbacks**: Invalidation callbacks should be fast; defer heavy work
5. **Consider update delayers**: Don't overwhelm consumers with too-frequent updates
