using Microsoft.EntityFrameworkCore;
using Samples.HelloCart.V2;
using static System.Console;

namespace Samples.HelloCart;

public abstract class AppBase
{
    public IServiceProvider ServerServices { get; protected set; } = null!;
    public IServiceProvider ClientServices { get; protected set; } = null!;
    public virtual IServiceProvider WatchedServices => ClientServices;

    public Product[] ExistingProducts { get; set; } = [];
    public Cart[] ExistingCarts { get; set; } = [];
    public bool MustRecreateDb { get; set; } = true;
    public int ExtraProductCount { get; set; } = 5;

    public virtual async Task InitializeAsync(IServiceProvider services, bool startHostedServices)
    {
        var pApple = new Product("apple", 2);
        var pBanana = new Product("banana", 0.5M);
        var pCarrot = new Product("carrot", 1);
        var cart1 = new Cart("cart1(apple=1,banana=2)") {
            Items = ImmutableDictionary<string, decimal>.Empty
                .Add(pApple.Id, 1)
                .Add(pBanana.Id, 2)
        };
        var cart2 = new Cart("cart2(banana=1,carrot=1)") {
            Items = ImmutableDictionary<string, decimal>.Empty
                .Add(pBanana.Id, 1)
                .Add(pCarrot.Id, 1)
        };
        var extraProducts = Enumerable.Range(0, ExtraProductCount)
            .Select(i => new Product($"product{i + 1}", i));
        ExistingProducts = [pApple, pBanana, pCarrot, ..extraProducts];
        ExistingCarts = [cart1, cart2];

        if (MustRecreateDb) {
            var dbContextFactory = services.GetService<IDbContextFactory<AppDbContext>>();
            if (dbContextFactory != null) {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync();
                await dbContext.Database.EnsureDeletedAsync();
                await dbContext.Database.EnsureCreatedAsync();
            }
            var commander = services.Commander();
            foreach (var product in ExistingProducts)
                await commander.Call(new EditCommand<Product>(product));
            foreach (var cart in ExistingCarts)
                await commander.Call(new EditCommand<Cart>(cart));
        }

        if (startHostedServices)
            await services.HostedServices().Start();
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (ClientServices is IAsyncDisposable csd)
            await csd.DisposeAsync();
        if (ServerServices is IAsyncDisposable sd)
            await sd.DisposeAsync();
    }

    public Task Watch(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();
        foreach (var product in ExistingProducts)
            tasks.Add(WatchProduct(services, product.Id, cancellationToken));
        foreach (var cart in ExistingCarts)
            tasks.Add(WatchCartTotal(services, cart.Id, cancellationToken));
        return Task.WhenAll(tasks);
    }

    public async Task WatchProduct(
        IServiceProvider services, string productId, CancellationToken cancellationToken = default)
    {
        var productService = services.GetRequiredService<IProductService>();
        var computed = await Computed.Capture(() => productService.Get(productId, cancellationToken), cancellationToken);
        while (true) {
            WriteLine($"  {computed.Value}");
            await computed.WhenInvalidated(cancellationToken);
            computed = await computed.Update(cancellationToken);
        }
    }

    public async Task WatchCartTotal(
        IServiceProvider services, string cartId, CancellationToken cancellationToken = default)
    {
        var cartService = services.GetRequiredService<ICartService>();
        var computed = await Computed.Capture(() => cartService.GetTotal(cartId, cancellationToken), cancellationToken);
        while (true) {
            WriteLine($"  {cartId}: total = {computed.Value}");
            await computed.WhenInvalidated(cancellationToken);
            computed = await computed.Update(cancellationToken);
        }
    }
}
