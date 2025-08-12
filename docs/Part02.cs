using System.Runtime.Serialization;
using ActualLab.Fusion.Server;
using ActualLab.IO;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using MemoryPack;
using MessagePack;
using Microsoft.AspNetCore.Builder;
using static System.Console;

namespace Docs;

#region Part02_CommonServices
// The interface for our chat service
public interface IChatService : IComputeService
{
    [ComputeMethod]
    Task<List<string>> GetRecentMessages(CancellationToken cancellationToken = default);

    [ComputeMethod]
    Task<int> GetWordCount(CancellationToken cancellationToken = default);

    Task Post(string message, CancellationToken cancellationToken = default);
}
#endregion

#region Part02_ServerImplementation
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
        var baseUrl = "http://localhost:22222/";
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
    }
    #endregion
}
