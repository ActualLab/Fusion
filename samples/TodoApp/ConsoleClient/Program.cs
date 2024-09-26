using ActualLab.Fusion.UI;
using Samples.TodoApp.Abstractions;
using static System.Console;

Write("Enter Session ID to use: ");
var sessionId = ReadLine()!.Trim();
var session = new Session(sessionId);

var services = CreateServiceProvider();
var todoApi = services.GetRequiredService<ITodoApi>();
var computed = await Computed.Capture(() => todoApi.GetSummary(session)).ConfigureAwait(false);
await foreach (var c in computed.Changes().ConfigureAwait(false))
    WriteLine($"- {c.Value}");

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

    // Default update delay is 0.2s
    services.AddScoped<IUpdateDelayer>(c => new UpdateDelayer(c.UIActionTracker(), 0.2));

    return services.BuildServiceProvider();
}
