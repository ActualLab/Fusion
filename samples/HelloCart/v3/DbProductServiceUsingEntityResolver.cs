using Samples.HelloCart.V2;
using ActualLab.Fusion.EntityFramework;

namespace Samples.HelloCart.V3;

// We inherit this service from a "normal" one to show the delta
// between it and IDbEntityResolver-based implementation.
public class DbProductServiceUsingEntityResolver(IServiceProvider services) : DbProductService(services)
{
    private IDbEntityResolver<string, DbProduct> ProductResolver { get; } =
        services.DbEntityResolver<string, DbProduct>();

    public override async Task<Product?> Get(string id, CancellationToken cancellationToken = default)
    {
        var dbProduct = await ProductResolver.Get(id, cancellationToken);
        return dbProduct is null
            ? null
            : new Product(dbProduct.Id, dbProduct.Price);
    }
}
