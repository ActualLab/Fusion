using ActualLab.Fusion.Operations.Internal;

namespace Samples.HelloCart.V1;

[DeferredInvalidationMode(DeferredInvalidationMode.Local)]
public class InMemoryProductService : IProductService
{
    private readonly ConcurrentDictionary<string, Product> _products = new();

    public virtual Task Edit(EditCommand<Product> command, CancellationToken cancellationToken = default)
    {
        var (productId, product) = command;
        if (string.IsNullOrEmpty(productId))
            throw new ArgumentOutOfRangeException(nameof(command));

        // This call triggers Operations Framework use for this command,
        // which is what makes the deferred invalidation below run once the change is committed.
        // Compare the invalidation logic here and in InMemoryCartService.Edit.
        TransientOperationScope.Require();
        if (product is null)
            _products.Remove(productId, out _);
        else
            _products[productId] = product;

        // Invalidation logic
        Invalidation.Defer(() => _ = Get(productId, default));
        return Task.CompletedTask;
    }

    public virtual Task<Product?> Get(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_products.GetValueOrDefault(id));
}
