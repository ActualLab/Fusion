# Streaming with RpcStream

`RpcStream<T>` enables efficient streaming of data over RPC connections.
Unlike traditional RPC where you return a complete list, streaming allows you to:

- Send data incrementally as it becomes available
- Handle large datasets without loading everything into memory
- Build real-time data feeds
- Nest streams within other data structures


## Why RpcStream Instead of IAsyncEnumerable?

You might wonder why ActualLab.Rpc uses a dedicated `RpcStream<T>` type instead of the standard `IAsyncEnumerable<T>`. The key reasons are:

1. **Serialization control**: ActualLab.Rpc supports multiple serialization formats &ndash; System.Text.Json, Newtonsoft.Json, MemoryPack, and MessagePack. Each has different serialization behavior, and `RpcStream<T>` provides custom converters for each format to ensure streams are serialized correctly as lightweight references rather than materialized collections.

2. **Bidirectional streaming**: Unlike `IAsyncEnumerable<T>` which is read-only, `RpcStream<T>` supports both server-to-client and client-to-server streaming. The same type works in both directions.

3. **Embedding in data structures**: `RpcStream<T>` can be embedded as a property in records and classes. When these objects are serialized, the stream is serialized as a reference ID &ndash; the actual stream data flows through a separate channel. This enables nested streams (streams containing objects that contain their own streams).

4. **Flow control**: `RpcStream<T>` has built-in acknowledgment-based backpressure (`AckPeriod` and `AckAdvance` properties) to prevent producers from overwhelming consumers.

5. **Reconnection handling**: `RpcStream<T>` integrates with ActualLab.Rpc's reconnection mechanism, allowing streams to resume after network interruptions.


## RpcStream API Overview

`RpcStream<T>` has a simple API:

| Member | Description |
|--------|-------------|
| `RpcStream.New<T>(IAsyncEnumerable<T>)` | Creates a stream from an async enumerable (server side) |
| `RpcStream.New<T>(IEnumerable<T>)` | Creates a stream from a synchronous enumerable |
| `GetAsyncEnumerator()` | Implements `IAsyncEnumerable<T>` for consumption |
| `AckPeriod` | How often the consumer sends acknowledgments (default: 30 items) |
| `AckAdvance` | How many items the producer can send ahead (default: 61 items) |

::: warning Single Enumeration
Remote streams can only be enumerated once. Attempting to enumerate a remote `RpcStream<T>` multiple times will throw an exception.
:::


## Creating an RpcStream (Server Side)

Use `RpcStream.New()` to create a stream from an `IAsyncEnumerable<T>` or `IEnumerable<T>`:

```cs
public interface ISimpleService : IRpcService
{
    // Returns a table with streaming rows
    Task<Table<int>> GetTable(string title, CancellationToken cancellationToken = default);
}

// A record containing an RpcStream
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial record Table<T>(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] string Title,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] RpcStream<Row<T>> Rows);

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial record Row<T>(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] int Index,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] RpcStream<T> Items);
```

The implementation creates streams from async enumerables:

```cs
public class SimpleService : ISimpleService
{
    public Task<Table<int>> GetTable(string title, CancellationToken cancellationToken = default)
    {
        // Create the table with a stream of rows
        var table = new Table<int>(title, RpcStream.New(GetRows(CancellationToken.None)));
        return Task.FromResult(table);
    }

    private async IAsyncEnumerable<Row<int>> GetRows(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; ; i++) {
            await Task.Delay(100, cancellationToken); // Simulate work
            // Each row contains its own stream of items
            yield return new Row<int>(i, RpcStream.New(GetItems(i, CancellationToken.None)));
        }
    }

    private async IAsyncEnumerable<int> GetItems(
        int index, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 0; ; i++) {
            await Task.Delay(50, cancellationToken); // Simulate work
            yield return index * i;
        }
    }
}
```


## Consuming an RpcStream (Client Side)

`RpcStream<T>` implements `IAsyncEnumerable<T>`, so you can use `await foreach`:

```cs
var table = await simpleService.GetTable("My Table", cancellationToken);
Console.WriteLine($"Table: {table.Title}");

await foreach (var row in table.Rows.WithCancellation(cancellationToken)) {
    Console.WriteLine($"Row {row.Index}:");

    await foreach (var item in row.Items.WithCancellation(cancellationToken)) {
        Console.WriteLine($"  Item: {item}");

        // Break early if you've seen enough
        if (item > 100)
            break;
    }
}
```


## Sending Streams to the Server

You can also send streams **from the client to the server**:

```cs
public interface ISimpleService : IRpcService
{
    // Server receives a stream from the client and computes the sum
    Task<int> Sum(RpcStream<int> stream, CancellationToken cancellationToken = default);
}
```

Server implementation:

```cs
public Task<int> Sum(RpcStream<int> stream, CancellationToken cancellationToken = default)
    => stream.SumAsync(cancellationToken).AsTask();
```

Client usage:

```cs
// Create a stream from local data
var numbers = new[] { 1, 2, 3, 4, 5 };
var stream = RpcStream.New(numbers);

// Send to server for processing
var sum = await simpleService.Sum(stream, cancellationToken);
Console.WriteLine($"Sum: {sum}"); // Output: Sum: 15
```


## RpcStream Serialization

`RpcStream<T>` can be embedded in any serializable record or class.
Use the standard serialization attributes:

```cs
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject(true)]
public sealed partial record Table<T>(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] string Title,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] RpcStream<Row<T>> Rows);
```

::: tip Nested Streams
RpcStream supports nesting &ndash; you can have streams of records that contain their own streams.
This is useful for hierarchical data like tables with rows, where each row has its own item stream.
:::


## Key Characteristics

| Feature | Behavior |
|---------|----------|
| **Direction** | Bidirectional &ndash; server-to-client and client-to-server |
| **Enumeration** | Remote streams can only be enumerated once |
| **Backpressure** | Built-in acknowledgment mechanism (configurable via `AckPeriod` and `AckAdvance`) |
| **Cancellation** | Streams can be cancelled from either end |
| **Nesting** | Streams can be nested within other data structures |
| **Reconnection** | Streams handle reconnection gracefully |


## Configuration Options

`RpcStream<T>` has two configurable properties for flow control:

| Property | Default | Description |
|----------|---------|-------------|
| `AckPeriod` | 30 | How often the client sends acknowledgments (every N items) |
| `AckAdvance` | 61 | How many items the server can send ahead before waiting for acks |

These defaults work well for most scenarios. Adjust them if you need different throughput/latency tradeoffs.


## Complete Example

See the [TodoApp RpcExamplePage](https://github.com/ActualLab/Fusion.Samples/blob/master/src/TodoApp/UI/Pages/RpcExamplePage.razor) for a complete working example demonstrating:
- Server-to-client streaming with nested streams
- Client-to-server streaming for computation
- Real-time UI updates as stream data arrives

The example streams a table where:
- Each row arrives incrementally
- Each row contains its own stream of items
- The client can compute sums by streaming data back to the server
