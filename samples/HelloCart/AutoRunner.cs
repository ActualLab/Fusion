using ActualLab.Resilience;
using static System.Console;

namespace Samples.HelloCart;

public static class AutoRunner
{
    public static async Task Run(AppBase app, CancellationToken cancellationToken = default)
    {
        ChaosMaker.Default.TryEnable();
        var productService = app.ClientServices.GetRequiredService<IProductService>();
        var commander = app.ClientServices.Commander();

        var rnd = new Random(10);
        for (var i = 0;; i++) {
            var productId = app.ExistingProducts[rnd.Next(app.ExistingProducts.Length)].Id;
            var product = await productService.Get(productId, cancellationToken);
            var price = rnd.Next(10);
            var command = new EditCommand<Product>(product! with { Price = price });
            WriteLine(command);
            _ = commander.Run(command, cancellationToken);
            await Task.Delay(500, cancellationToken);
        }
    }
}
