using System.Reactive;
using System.Runtime.Serialization;
using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.Concurrency;
using ActualLab.Fusion;
using ActualLab.Fusion.Server;
using ActualLab.IO;
using ActualLab.Rpc;
using ActualLab.Rpc.Server;
using MemoryPack;
using static System.Console;

#pragma warning disable ASP0000

var useLogging = false;
var logRpcCalls = false;

var baseUrl = "http://localhost:22222/";
await (args switch {
    ["server"] => RunServer(),
    ["client"] => RunClient(),
    _ => Task.WhenAll(RunServer(), RunClient()),
});

async Task RunServer()
{
    var builder = WebApplication.CreateBuilder();
    if (useLogging)
        builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Debug).AddConsole();
    builder.Services.AddFusion(RpcServiceMode.Server, fusion => {
        fusion.AddWebServer();
        if (logRpcCalls)
            fusion.Services.AddSingleton<RpcPeerFactory>(_ =>
                static (hub, peerRef) => !peerRef.IsServer
                    ? throw new NotSupportedException("No client peers are allowed on the server.")
                    : new RpcServerPeer(hub, peerRef) { CallLogLevel = LogLevel.Debug }
            );
        fusion.AddService<IChat, Chat>();
    });
    var app = builder.Build();

    app.UseWebSockets();
    app.MapRpcWebSocketServer();
    try {
        await app.RunAsync(baseUrl);
    }
    catch (Exception error) {
        Error.WriteLine($"Server failed: {error.Message}");
    }
}

async Task RunClient()
{
    var services = new ServiceCollection()
        .AddLogging(logging => {
            logging.ClearProviders();
            if (useLogging)
                logging.SetMinimumLevel(LogLevel.Debug).AddConsole();
        })
        .AddFusion(fusion => {
            fusion.Rpc.AddWebSocketClient(baseUrl);
            if (logRpcCalls)
                fusion.Services.AddSingleton<RpcPeerFactory>(_ =>
                    static (hub, peerRef) => peerRef.IsServer
                        ? throw new NotSupportedException("No server peers are allowed on the client.")
                        : new RpcClientPeer(hub, peerRef) { CallLogLevel = LogLevel.Debug }
                );
            fusion.AddClient<IChat>();
        })
        .BuildServiceProvider();

    var chat = services.GetRequiredService<IChat>();
    var commander = services.Commander();
    _ = Task.Run(ObserveMessages);
    _ = Task.Run(ObserveWordCount);
    while (true) {
        var message = await ConsoleExt.ReadLineAsync() ?? "";
        try {
            await commander.Call(new Chat_Post(message));
        }
        catch (Exception error) {
            Error.WriteLine($"Error: {error.Message}");
        }
    }

    async Task ObserveMessages() {
        var cMessages = await Computed.Capture(() => chat.GetRecentMessages());
        await foreach (var (messages, _, version) in cMessages.Changes()) {
            WriteLine($"Messages changed (version: {version}):");
            foreach (var message in messages)
                WriteLine($"- {message}");
        }
    };

    async Task ObserveWordCount() {
        var cMessageCount = await Computed.Capture(() => chat.GetWordCount());
        await foreach (var (wordCount, _) in cMessageCount.Changes())
            WriteLine($"Word count changed: {wordCount}");
    };
}

public interface IChat : IComputeService
{
    [ComputeMethod]
    Task<List<string>> GetRecentMessages(CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<int> GetWordCount(CancellationToken cancellationToken = default);

    [CommandHandler]
    Task Post(Chat_Post command, CancellationToken cancellationToken);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public sealed partial record Chat_Post(
    [property: DataMember, MemoryPackOrder(0)] string Message
) : ICommand<Unit>;

public class Chat : IChat
{
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

    public virtual Task Post(Chat_Post command, CancellationToken cancellationToken)
    {
        lock (_lock) {
            var posts = _posts.ToList(); // We can't update the list itself (it's shared), but can re-create it
            posts.Add(command.Message);
            if (posts.Count > 10)
                posts.RemoveAt(0);
            _posts = posts;
        }

        using var _1 = Invalidation.Begin();
        _ = GetRecentMessages(default); // No need to invalidate GetWordCount
        return Task.CompletedTask;
    }
}
