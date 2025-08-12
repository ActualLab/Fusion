using System.Diagnostics;
using ActualLab.Fusion.Server;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using Microsoft.AspNetCore.Builder;
using static System.Console;

namespace Docs;

#region Part02_CommonServices
// The interface for our chat service
public interface IChatService : IComputeService
{
    // Compute methods - they'll cache the output not only on the server side
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

    public virtual Task<List<string>> GetRecentMessages(CancellationToken cancellationToken = default)
        => Task.FromResult(_posts);

    public virtual async Task<int> GetWordCount(CancellationToken cancellationToken = default)
    {
        // NOTE: GetRecentMessages() is a compute method, so GetWordCount() call becomes dependent on it,
        // and that's why it gets invalidated automatically when GetRecentMessages() is invalidated.
        var messages = await GetRecentMessages(cancellationToken).ConfigureAwait(false);
        return messages
            .Select(m => m.Split(" ", StringSplitOptions.RemoveEmptyEntries).Length)
            .Sum();
    }

    public Task<int> GetWordCountPlainRpc(CancellationToken cancellationToken = default)
        => GetWordCount(cancellationToken);

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
        _ = GetRecentMessages(default); // No need to invalidate GetWordCount(), coz it depends on GetRecentMessages()
        return Task.CompletedTask;
    }
}
#endregion

public static class Part02
{
    #region Part02_ServerSetup
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
    #endregion

    #region Part02_ClientSetup
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
    #endregion

    #region Part02_ClientUsage
    public static async Task Run()
    {
        await (new[] { "both" } switch {
            ["server"] => RunServer(),
            ["client"] => RunClient(),
            _ => Task.WhenAll(RunServer(), RunClient()),
        });
    }

    public static async Task RunServer()
    {
        var app = CreateHost();
        try {
            await app.RunAsync("http://localhost:22222/");
        }
        catch (Exception error) {
            Error.WriteLine($"Server failed: {error.Message}");
        }
    }

    public static async Task RunClient()
    {
        // Create client services
        var services = CreateClientServices("http://localhost:22222/");
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

        // Remote compute method call vs plain RPC call performance comparison
        WriteLine("100K calls to GetWordCount() vs GetWordCountPlainRpc() - run in Release!");
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

        100K calls to GetWordCount() vs GetWordCountPlainRpc() - run in Release!
        - Warmup...
        - Benchmarking...
        - GetWordCount():         12.187ms
        - GetWordCountPlainRpc(): 2.474s
        */
    }
    #endregion
}
