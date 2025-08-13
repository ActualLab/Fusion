using System.Diagnostics;
using ActualLab.Fusion.Server;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using Microsoft.AspNetCore.Builder;
using static System.Console;

namespace Docs;

#region Part02_SharedApi
// The interface for our chat service
public interface IChatService : IComputeService
{
    // Compute methods - they cache the output not only on the server side
    // but on the client side as well!
    [ComputeMethod]
    Task<List<string>> GetRecentMessages(CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<int> GetWordCount(CancellationToken cancellationToken = default);

    // Regular methods
    Task Post(string message, CancellationToken cancellationToken = default);
    Task<int> GetWordCountPlainRpc(CancellationToken cancellationToken = default);
}
#endregion

#region Part02_ServerImplementation
public class ChatService : IChatService
{
    private readonly Lock _lock = new();
    private List<string> _posts = new();

    // It's a [ComputeMethod] method -> it has to be virtual to allow Fusion to override it
    public virtual Task<List<string>> GetRecentMessages(CancellationToken cancellationToken = default)
        => Task.FromResult(_posts);

    // It's a [ComputeMethod] method -> it has to be virtual to allow Fusion to override it
    public virtual async Task<int> GetWordCount(CancellationToken cancellationToken = default)
    {
        // NOTE: GetRecentMessages() is a compute method, so the GetWordCount() call becomes dependent on it,
        // and that's why it gets invalidated automatically when GetRecentMessages() is invalidated.
        var messages = await GetRecentMessages(cancellationToken).ConfigureAwait(false);
        return messages
            .Select(m => m.Split(" ", StringSplitOptions.RemoveEmptyEntries).Length)
            .Sum();
    }

    // Regular method
    public Task<int> GetWordCountPlainRpc(CancellationToken cancellationToken = default)
        => GetWordCount(cancellationToken);

    // Regular method
    public Task Post(string message, CancellationToken cancellationToken = default)
    {
        lock (_lock) {
            var posts = _posts.ToList(); // We can't update the list itself (it's shared), but we can re-create it
            posts.Add(message);
            if (posts.Count > 10)
                posts.RemoveAt(0);
            _posts = posts;
        }

        using var _1 = Invalidation.Begin();
        _ = GetRecentMessages(default); // No need to invalidate GetWordCount(), coz it depends on GetRecentMessages()
        return Task.CompletedTask;
    }
}
#endregion

public static class Part02
{
    public static async Task Run()
    {
        using var stopTokenSource = new CancellationTokenSource();
        var serverTask = RunServer(stopTokenSource.Token);
        await RunClient();
        await stopTokenSource.CancelAsync();
        await serverTask;
    }

    public static WebApplication CreateHost()
    {
        #region Part02_ServerSetup
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Debug).AddConsole();

        // Adding Fusion.
        // RpcServiceMode.Server is going to be the default mode for further `fusion.AddService()` calls,
        // which means that any compute service added via `fusion.AddService()` will be shared via RPC as well.
        var fusion = builder.Services.AddFusion(RpcServiceMode.Server);
        fusion.AddWebServer(); // Adds the RPC server middleware
        fusion.AddService<IChatService, ChatService>(RpcServiceMode.Server); // Adds the chat service impl. (Compute Service)

        var app = builder.Build();
        app.UseWebSockets(); // Enable WebSockets support on Kestrel server
        app.MapRpcWebSocketServer(); // Map the ActualLab.Rpc WebSocket server endpoint ("/rpc/ws")
        #endregion
        return app;
    }

    public static IServiceProvider CreateClientServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Debug).AddConsole();
        });

        #region Part02_ClientSetup
        var fusion = services.AddFusion(); // No default RpcServiceMode, so it will be set to RpcServiceMode.Local
        var rpc = fusion.Rpc; // The same as services.AddRpc(), but slightly faster, since FusionBuilder already did it
        rpc.AddWebSocketClient("http://localhost:22222/"); // Adds the WebSocket client for ActualLab.Rpc
        fusion.AddClient<IChatService>(); // Adds the chat service client (Compute Service Client)
        #endregion

        return services.BuildServiceProvider();
    }

    public static async Task RunServer(CancellationToken cancellationToken = default)
    {
        var app = CreateHost();
        #region Part02_RunServer
        try {
            await app.RunAsync("http://localhost:22222/").WaitAsync(cancellationToken);
        }
        catch (Exception error) {
            if (error.IsCancellationOf(cancellationToken))
                await app.StopAsync();
            else
                Error.WriteLine($"Server failed: {error.Message}");
        }
        #endregion
    }

    public static async Task RunClient()
    {
        #region Part02_RunClient
        var services = CreateClientServiceProvider();
        var chatClient = services.GetRequiredService<IChatService>();

        // Start GetWordCount() change observer
        var cWordCount0 = await Computed.Capture(() => chatClient.GetWordCount());
        _ = Task.Run(async () => {
            await foreach (var cWordCount in cWordCount0.Changes())
                WriteLine($"GetWordCount() -> {cWordCount}, Value: {cWordCount.Value}");
        });

        // Start GetRecentMessages() change observer
        var cMessages0 = await Computed.Capture(() => chatClient.GetRecentMessages());
        _ = Task.Run(async () => {
            await foreach (var cMessages in cMessages0.Changes()) {
                await Task.Delay(25); // We delay the output to print GetWordCount() first
                WriteLine($"GetRecentMessages() -> {cMessages}, Value:");
                foreach (var message in cMessages.Value)
                    WriteLine($"- {message}");
                WriteLine();
            }
        });

        // Post some messages
        await chatClient.Post("Hello, World!");
        await Task.Delay(100);
        await chatClient.Post("Let's count to 3!");
        string[] data = ["One", "Two", "Three"];
        for (var i = 1; i <= 3; i++) {
            await Task.Delay(1000);
            await chatClient.Post(data.Take(i).ToDelimitedString());
        }
        await Task.Delay(1000);
        await chatClient.Post("Done counting!");
        await Task.Delay(1000);
        #endregion

        #region Part02_Output
        /* The output:
        GetWordCount() -> RemoteComputed<Int32>(*IChatService.GetWordCount(ct-none)-Hash=14783957 v.4u, State: Consistent), Value: 0
        GetRecentMessages() -> RemoteComputed<List<String>>(*IChatService.GetRecentMessages(ct-none)-Hash=14783978 v.d5, State: Invalidated), Value:

        GetWordCount() -> RemoteComputed<Int32>(*IChatService.GetWordCount(ct-none)-Hash=14783957 v.h5, State: Consistent), Value: 2
        GetRecentMessages() -> RemoteComputed<List<String>>(*IChatService.GetRecentMessages(ct-none)-Hash=14783978 v.l5, State: Consistent), Value:
        - Hello, World!

        GetWordCount() -> RemoteComputed<Int32>(*IChatService.GetWordCount(ct-none)-Hash=14783957 v.p5, State: Consistent), Value: 6
        GetRecentMessages() -> RemoteComputed<List<String>>(*IChatService.GetRecentMessages(ct-none)-Hash=14783978 v.4q, State: Consistent), Value:
        - Hello, World!
        - Let's count to 3!

        GetWordCount() -> RemoteComputed<Int32>(*IChatService.GetWordCount(ct-none)-Hash=14783957 v.d0, State: Consistent), Value: 7
        GetRecentMessages() -> RemoteComputed<List<String>>(*IChatService.GetRecentMessages(ct-none)-Hash=14783978 v.cp, State: Consistent), Value:
        - Hello, World!
        - Let's count to 3!
        - One

        GetWordCount() -> RemoteComputed<Int32>(*IChatService.GetWordCount(ct-none)-Hash=14783957 v.4v, State: Consistent), Value: 9
        GetRecentMessages() -> RemoteComputed<List<String>>(*IChatService.GetRecentMessages(ct-none)-Hash=14783978 v.gp, State: Consistent), Value:
        - Hello, World!
        - Let's count to 3!
        - One
        - One, Two

        GetWordCount() -> RemoteComputed<Int32>(*IChatService.GetWordCount(ct-none)-Hash=14783957 v.ou, State: Consistent), Value: 12
        GetRecentMessages() -> RemoteComputed<List<String>>(*IChatService.GetRecentMessages(ct-none)-Hash=14783978 v.8v, State: Consistent), Value:
        - Hello, World!
        - Let's count to 3!
        - One
        - One, Two
        - One, Two, Three

        GetWordCount() -> RemoteComputed<Int32>(*IChatService.GetWordCount(ct-none)-Hash=14783957 v.h0, State: Consistent), Value: 14
        GetRecentMessages() -> RemoteComputed<List<String>>(*IChatService.GetRecentMessages(ct-none)-Hash=14783978 v.kp, State: Consistent), Value:
        - Hello, World!
        - Let's count to 3!
        - One
        - One, Two
        - One, Two, Three
        - Done counting!
        */
        #endregion

        #region Part02_Benchmark
        // Benchmarking remote compute method calls and plain RPC calls - run in Release mode!
        WriteLine("100K calls to GetWordCount() vs GetWordCountPlainRpc():");
        WriteLine("- Warmup...");
        for (int i = 0; i < 100_000; i++)
            await chatClient.GetWordCount().ConfigureAwait(false);
        for (int i = 0; i < 100_000; i++)
            await chatClient.GetWordCountPlainRpc().ConfigureAwait(false);
        WriteLine("- Benchmarking...");
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 100_000; i++)
            await chatClient.GetWordCount().ConfigureAwait(false);
        WriteLine($"- GetWordCount():         {stopwatch.Elapsed.ToShortString()}");
        stopwatch.Restart();
        for (int i = 0; i < 100_000; i++)
            await chatClient.GetWordCountPlainRpc().ConfigureAwait(false);
        WriteLine($"- GetWordCountPlainRpc(): {stopwatch.Elapsed.ToShortString()}");
        #endregion

        #region Part02_Benchmark_Output
        /* The output:
        100K calls to GetWordCount() vs GetWordCountPlainRpc() - run in Release!
        - Warmup...
        - Benchmarking...
        - GetWordCount():         12.187ms
        - GetWordCountPlainRpc(): 2.474s
        */
        #endregion
    }
}
