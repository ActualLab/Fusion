using ActualLab.Fusion.EntityFramework;

namespace Samples.HelloCart.V2;

public class DbProductService(IServiceProvider services)
    : DbServiceBase<AppDbContext>(services), IProductService
{
    public virtual async Task Edit(EditCommand<Product> command, CancellationToken cancellationToken = default)
    {
        var (productId, product) = command;
        if (string.IsNullOrEmpty(productId))
            throw new ArgumentOutOfRangeException(nameof(command));

        if (Invalidation.IsActive) {
            _ = Get(productId, default);
            return;
        }

        await using var dbContext = await DbHub.CreateOperationDbContext(cancellationToken);
        var dbProduct = await dbContext.Products.FindAsync(DbKey.Compose(productId), cancellationToken);
        if (product is null) {
            if (dbProduct is not null)
                dbContext.Remove(dbProduct);
        }
        else {
            if (dbProduct is not null)
                dbProduct.Price = product.Price;
            else
                dbContext.Add(new DbProduct { Id = productId, Price = product.Price });
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // Adding LogMessageCommand as an event
        var context = CommandContext.GetCurrent();
        var message = product is null
            ? $"Product removed: {productId}"
            : $"Product updated: {productId} with Price = {product.Price}";
        var logEvent = new LogMessageCommand(Ulid.NewUlid().ToString(), message);
        context.Operation.AddEvent(logEvent);
        var randomEvent = LogMessageCommand.New();
        context.Operation.AddEvent(randomEvent).SetDelayUntil(randomEvent.DelayUntil);
    }

    public virtual async Task<Product?> Get(string id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await DbHub.CreateDbContext(cancellationToken);
        var dbProduct = await dbContext.Products.FindAsync(DbKey.Compose(id), cancellationToken);
        if (dbProduct is null)
            return null;

        return new Product(dbProduct.Id, dbProduct.Price);
    }
}
