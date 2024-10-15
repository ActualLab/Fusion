using ActualLab.Fusion.UI;
using Samples.TodoApp.Abstractions;
using static System.Console;

#pragma warning disable MA0004 // Use .ConfigureAwait(...)

Write("Enter Session ID to use: ");
var sessionId = ReadLine()!.Trim();
var session = new Session(sessionId);

var services = CreateServiceProvider();
var todoApi = services.GetRequiredService<ITodoApi>();
await ObserveTodos();
// await ObserveSummary();

async Task ObserveTodos()
{
    var computed = await Computed.New(services, async ct => {
        var itemIds = await todoApi.ListIds(session, int.MaxValue, ct).ConfigureAwait(false);
        var items = await itemIds.Select(id => todoApi.Get(session, id, ct)).Collect(ct).ConfigureAwait(false);
        return items;
    }).Update();
    await foreach (var (items, error) in computed.Changes()) {
        WriteLine($"Todos ({items.Length}):");
        foreach (var item in items)
            WriteLine($"- {item}");
    }
}

async Task ObserveSummary()
{
    var computed = await Computed.Capture(() => todoApi.GetSummary(session));
    await foreach (var c in computed.Changes())
        WriteLine($"- {c.Value}");
}

IServiceProvider CreateServiceProvider()
{
    // ReSharper disable once VariableHidesOuterVariable
    var services = new ServiceCollection();
    services.AddLogging(logging => {
        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Warning);
        logging.AddConsole();
    });

    var fusion = services.AddFusion();
    fusion.Rpc.AddWebSocketClient("http://localhost:5005");
    fusion.AddAuthClient();
    fusion.AddClient<ITodoApi>(); // Compute service client
    fusion.Rpc.AddClient<ISimpleService>(); // Simple RPC service client
    services.AddScoped<IUpdateDelayer>(
        c => new UpdateDelayer(c.UIActionTracker(), 0.2)); // Default update delay is 0.2s

    return services.BuildServiceProvider();
}
