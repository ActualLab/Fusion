using ActualLab.IO;
using ActualLab.Resilience;
using Samples.HelloCart;
using Samples.HelloCart.V1;
using Samples.HelloCart.V2;
using Samples.HelloCart.V3;
using Samples.HelloCart.V4;
using Samples.HelloCart.V5;
using static System.Console;

if (AppSettings.Db.UseChaosMaker) {
    var dbChaosMaker = AppSettings.Db.ChaosMaker;
    WriteLine(dbChaosMaker);
    ChaosMaker.Default = dbChaosMaker;
}

// Create services
AppBase? app;
while(true) {
    WriteLine("Select the implementation to use:");
    WriteLine("  1: ConcurrentDictionary-based");
    WriteLine("  2: EF Core + Operations Framework (OF)");
    WriteLine("  3: EF Core + OF + DbEntityResolvers (pipelined fetches)");
    WriteLine("  4: EF Core + OF + DbEntityResolvers + Client-Server");
    WriteLine("  5: EF Core + OF + DbEntityResolvers + Client-Server + Multi-Host");
    // WriteLine("  4: 3 + client-server mode");
    Write("Type 1..5: ");
    var input = args.SingleOrDefault();
    if (input is not null)
        WriteLine(input);
    else
        input = await ConsoleExt.ReadLineAsync() ?? "";
    app = input.Trim() switch {
        "1" => new AppV1(),
        "2" => new AppV2(),
        "3" => new AppV3(),
        "4" => new AppV4(),
        "5" => new AppV5(),
        _ => null,
    };
    if (app is not null)
        break;
    WriteLine("Invalid selection.");
    WriteLine();
}
await using var appDisposable = app;
await app.InitializeAsync(app.ServerServices, true);

// Starting watch tasks
WriteLine("Initial state:");
using var cts = new CancellationTokenSource();
_ = app.Watch(app.WatchedServices, cts.Token);
await Task.Delay(700); // Just to make sure watch tasks print whatever they want before our prompt appears
if (AppSettings.UseAutoRunner)
    await AutoRunner.Run(app); // This method call never ends

var productService = app.ClientServices.GetRequiredService<IProductService>();
var commander = app.ClientServices.Commander();
WriteLine();
WriteLine("Change product price by typing [productId]=[price], e.g. \"apple=0\".");
WriteLine("See the total of every affected cart changes.");
while (true) {
    await Task.Delay(500);
    WriteLine();
    Write("[productId]=[price]: ");
    try {
        var input = (await ConsoleExt.ReadLineAsync() ?? "").Trim();
        if (input == "")
            break;
        var parts = input.Split("=");
        if (parts.Length != 2)
            throw new ApplicationException("Invalid price expression.");

        var productId = parts[0].Trim();
        var price = decimal.Parse(parts[1].Trim());
        var product = await productService.Get(productId);
        if (product is null)
            throw new KeyNotFoundException("Specified product doesn't exist.");

        var command = new EditCommand<Product>(product with { Price = price });
        await commander.Call(command);
        // You can run absolutely identical action with:
        // await app.ClientServices.Commander().Call(command);
    }
    catch (Exception e) {
        WriteLine($"Error: {e.Message}");
    }
}
WriteLine("Terminating...");
cts.Cancel();
