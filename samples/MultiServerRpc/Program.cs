using ActualLab.Fusion.Rpc;
using Samples.MultiServerRpc;
using ActualLab.Fusion.Server;
using ActualLab.IO;
using ActualLab.Mathematics;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Server;
using Microsoft.AspNetCore.Builder;
using static System.Console;

#pragma warning disable ASP0000

const int serverCount = 2;
var serverUrls = Enumerable.Range(0, serverCount).Select(i => $"http://localhost:{22222 + i}/").ToArray();
var clientPeerRefs = Enumerable.Range(0, serverCount).Select(i => RpcPeerRef.NewClient(serverUrls[i])).ToArray();

await (args switch {
    ["server"] => RunServers(),
    ["client"] => RunClient(),
    _ => Task.WhenAll(RunServers(), RunClient()),
});

Task RunServers()
    => Task.WhenAll(Enumerable.Range(0, serverCount).Select(RunServer));

async Task RunServer(int serverIndex)
{
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders().AddDebug();
    builder.Services
        .AddSingleton(_ => new ServerId($"Server{serverIndex}"))
        .AddFusion(RpcServiceMode.Server, fusion => {
            fusion.AddWebServer();
            fusion.AddService<IChat, Chat>();
        });
    var app = builder.Build();

    app.UseWebSockets();
    app.MapRpcWebSocketServer();
    try {
        await app.RunAsync(serverUrls[serverIndex]);
    }
    catch (Exception error) {
        Error.WriteLine($"Server failed: {error.Message}");
    }
}

async Task RunClient()
{
    var services = new ServiceCollection()
        .AddFusion(fusion => {
            fusion.Rpc.AddWebSocketClient();
            fusion.AddClient<IChat>();
        })
        .AddSingleton(_ => FusionRpcOptionOverrides.DefaultOutboundCallOptions with {
            RouterFactory = method => args => {
                if (method.Kind is RpcMethodKind.Command && Invalidation.IsActive)
                    return RpcPeerRef.Local; // Commands in invalidation mode must always execute locally

                if (method.Service.Type == typeof(IChat)) {
                    var arg0Type = args.GetType(0);
                    int hash;
                    if (arg0Type == typeof(string))
                        // Contrary to string.GetHashCode, GetXxHash3 doesn't change run to run
                        hash = args.Get<string>(0).GetXxHash3();
                    else if (arg0Type == typeof(Chat_Post))
                        hash = args.Get<Chat_Post>(0).ChatId.GetXxHash3();
                    else
                        throw new NotSupportedException("Can't route this call.");
                    return clientPeerRefs[hash.PositiveModulo(serverCount)];
                }
                return RpcPeerRef.Default;
            }
        })
        .BuildServiceProvider();

    Write("Enter chat ID: ");
    var chatId = (await ConsoleExt.ReadLineAsync() ?? "").Trim();
    var chat = services.GetRequiredService<IChat>();
    var commander = services.Commander();
    _ = Task.Run(ObserveMessages);
    _ = Task.Run(ObserveWordCount);
    while (true) {
        var message = await ConsoleExt.ReadLineAsync() ?? "";
        try {
            await commander.Call(new Chat_Post(chatId, message));
        }
        catch (Exception error) {
            Error.WriteLine($"Error: {error.Message}");
        }
    }

    async Task ObserveMessages() {
        var cMessages = await Computed.Capture(() => chat.GetRecentMessages(chatId));
        await foreach (var (messages, _, version) in cMessages.Changes()) {
            WriteLine($"Messages changed (version: {version}):");
            foreach (var message in messages)
                WriteLine($"- {message}");
        }
    }

    async Task ObserveWordCount() {
        var cMessageCount = await Computed.Capture(() => chat.GetWordCount(chatId));
        await foreach (var (wordCount, _) in cMessageCount.Changes())
            WriteLine($"Word count changed: {wordCount}");
    }
}
