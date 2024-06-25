using ActualLab.Fusion.Operations.Internal;

namespace Samples.HelloCart.V1;

public class InMemoryProductService : IProductService
{
    private readonly ConcurrentDictionary<string, Product> _products = new();

    public virtual Task Edit(EditCommand<Product> command, CancellationToken cancellationToken = default)
    {
        var (productId, product) = command;
        if (string.IsNullOrEmpty(productId))
            throw new ArgumentOutOfRangeException(nameof(command));

        if (Invalidation.IsActive) {
            _ = Get(productId, default);
            return Task.CompletedTask;
        }

        InMemoryOperationScope.Require();
        if (product == null)
            _products.Remove(productId, out _);
        else
            _products[productId] = product;
        return Task.CompletedTask;
    }

    public virtual Task<Product?> Get(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_products.GetValueOrDefault(id));
}
