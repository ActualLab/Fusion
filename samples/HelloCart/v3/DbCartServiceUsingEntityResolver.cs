using Samples.HelloCart.V2;
using ActualLab.Fusion.EntityFramework;

namespace Samples.HelloCart.V3;

// We inherit this service from a "normal" one to show the delta
// between it and IDbEntityResolver-based implementation.
public class DbCartServiceUsingEntityResolver(IServiceProvider services)
    : DbCartService(services)
{
    private IDbEntityResolver<string, DbCart> CartResolver { get; } = services.DbEntityResolver<string, DbCart>();

    public override async Task<Cart?> Get(string id, CancellationToken cancellationToken = default)
    {
        var dbCart = await CartResolver.Get(id, cancellationToken);
        return dbCart == null ? null : new Cart(dbCart.Id) {
            Items = dbCart.Items.ToImmutableDictionary(i => i.DbProductId, i => i.Quantity),
        };
    }

    public override async Task<decimal> GetTotal(string id, CancellationToken cancellationToken = default)
    {
        var cart = await Get(id, cancellationToken);
        if (cart == null)
            return 0;

        var itemTotals = await Task.WhenAll(cart.Items.Select(async item => {
            var product = await Products.Get(item.Key, cancellationToken);
            return item.Value * (product?.Price ?? 0M);
        }));

        return itemTotals.Sum();
    }
}
