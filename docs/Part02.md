# Compute Service Clients

Compute Service Clients are remote proxies of Compute Services that take the behavior of `Computed<T>` into account to be more efficient than identical web API clients.

They provide several key benefits:

1. **Consistent Behavior**: They similarly back the result to any call with `Computed<T>` that mimics matching `Computed<T>` on the server side. This means client-side proxies can be used in other client-side Compute Services, and invalidation of a server-side dependency will trigger invalidation of its client-side replica.

2. **Efficient Caching**: They similarly cache consistent replicas. In other words, Compute Service clients won't make a remote call in case a *consistent* replica is still available. This provides exactly the same behavior as for Compute Services if we replace "computation" with "RPC call".

3. **Automatic Invalidation**: Client-side computed values are automatically invalidated when their server-side counterparts are invalidated, ensuring data consistency across the network.

Compute Service clients communicate with the server over WebSocket channels - internally they use `ActualLab.Rpc` infrastructure to make such calls, as well as to receive notifications about server-side invalidations.

Resilience features like reconnect on disconnect and refresh of every replica of `Computed<T>` on reconnect are bundled - `ActualLab.Rpc` and `ActualLab.Fusion.Client` take care of that.

## Creating a Compute Service Client

Let's create a simple chat service that demonstrates how Compute Service Clients work. We'll need both server and client implementations.

### 1. Common Interface

First, we define a common interface that both the server-side service and client-side proxy will implement:

<!-- snippet: Part02_CommonServices -->
```cs
// The interface for our chat service
public interface IChatService : IComputeService
{
    [ComputeMethod]
    Task<List<string>> GetRecentMessages(CancellationToken cancellationToken = default);
    
    [ComputeMethod]
    Task<int> GetWordCount(CancellationToken cancellationToken = default);
    
    Task Post(string message, CancellationToken cancellationToken = default);
}
```
<!-- endSnippet -->

### 2. Server-Side Implementation

Now let's implement the server-side service:

<!-- snippet: Part02_ServerImplementation -->
```cs
public class ChatService : IChatService
{
    // ReSharper disable once ChangeFieldTypeToSystemThreadingLock
    private readonly object _lock = new();
    private List<string> _posts = new();

    public virtual Task<List<string>> GetRecentMessages(CancellationToken cancellationToken = default)
        => Task.FromResult(_posts);

    public virtual async Task<int> GetWordCount(CancellationToken cancellationToken = default)
    {
        // Note that GetRecentMessages call here becomes a dependency of WordCount call,
        // and that's why it gets invalidated automatically.
        var messages = await GetRecentMessages(cancellationToken).ConfigureAwait(false);
        return messages
            .Select(m => m.Split(" ", StringSplitOptions.RemoveEmptyEntries).Length)
            .Sum();
    }

    public virtual Task Post(string message, CancellationToken cancellationToken = default)
    {
        lock (_lock) {
            var posts = _posts.ToList(); // We can't update the list itself (it's shared), but can re-create it
            posts.Add(message);
            if (posts.Count > 10)
                posts.RemoveAt(0);
            _posts = posts;
        }

        using var _1 = Invalidation.Begin();
        _ = GetRecentMessages(default); // No need to invalidate GetWordCount
        return Task.CompletedTask;
    }
}
```
<!-- endSnippet -->

### 3. Server Setup

To host our service, we need to set up a web server:

<!-- snippet: Part02_ServerSetup -->
```cs
public static WebApplication CreateHost()
{
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Debug).AddConsole();
    builder.Services.AddFusion(RpcServiceMode.Server, fusion => {
        fusion.AddWebServer();
        fusion.AddService<IChatService, ChatService>();
    });
    
    var app = builder.Build();
    app.UseWebSockets();
    app.MapRpcWebSocketServer();
    return app;
}
```
<!-- endSnippet -->

### 4. Client Setup

On the client side, we need to set up the service provider with the client proxy:

<!-- snippet: Part02_ClientSetup -->
```cs
public static IServiceProvider CreateClientServices(string baseUrl)
{
    var services = new ServiceCollection()
        .AddLogging(logging => {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug).AddConsole();
        })
        .AddFusion(fusion => {
            fusion.Rpc.AddWebSocketClient(baseUrl);
            fusion.AddClient<IChatService>();
        });
    return services.BuildServiceProvider();
}
```
<!-- endSnippet -->

### 5. Using the Client

Now we can use our client to interact with the server:

<!-- snippet: Part02_ClientUsage -->
```cs
// Create and start the server
var app = CreateHost();
await app.StartAsync("http://localhost:22222/");
WriteLine("Host started.");

// Create client services
var services = CreateClientServices("http://localhost:22222/");
var chat = services.GetRequiredService<IChatService>();

// Observe messages
var cMessages = await Computed.Capture(() => chat.GetRecentMessages());
_ = Task.Run(async () => {
    await foreach (var (messages, _, version) in cMessages.Changes()) {
        WriteLine($"Messages changed (version: {version}):");
        foreach (var message in messages)
            WriteLine($"- {message}");
    }
});

// Observe word count
var cWordCount = await Computed.Capture(() => chat.GetWordCount());
_ = Task.Run(async () => {
    await foreach (var (wordCount, _) in cWordCount.Changes())
        WriteLine($"Word count changed: {wordCount}");
});

// Post some messages
await chat.Post("Hello, World!");
await Task.Delay(1000);
await chat.Post("This is a test message.");
await Task.Delay(1000);
await chat.Post("Another message for testing.");

await Task.Delay(2000);
await app.StopAsync();
```
<!-- endSnippet -->

## How It Works

When you run this example, you'll see that:

1. The client automatically caches consistent replicas of computed values
2. When a command is executed on the server, the relevant computed values are invalidated on the client
3. The client automatically refreshes its cached values when they become invalidated
4. The WebSocket connection provides real-time updates with automatic reconnection

This approach provides a seamless experience where client-side code can work with remote services almost as if they were local, while still benefiting from efficient caching and automatic invalidation.

#### [Next: Part 03 &raquo;](./Part03.md) | [Documentation Home](./README.md)