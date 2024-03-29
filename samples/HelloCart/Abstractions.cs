using System.Runtime.Serialization;
using MemoryPack;
using Newtonsoft.Json;

namespace Samples.HelloCart;

[DataContract, MemoryPackable]
[method: JsonConstructor, MemoryPackConstructor]
public partial record Product(
    [property: DataMember] string Id,
    [property: DataMember] decimal Price
) : IHasId<string>;

[DataContract, MemoryPackable]
[method: JsonConstructor, MemoryPackConstructor]
public partial record Cart(
    [property: DataMember] string Id
) : IHasId<string>
{
    [DataMember] public ImmutableDictionary<string, decimal> Items { get; init; } = ImmutableDictionary<string, decimal>.Empty;
}

[DataContract, MemoryPackable]
[method: JsonConstructor, MemoryPackConstructor]
public partial record EditCommand<TItem>(
    [property: DataMember] string Id,
    [property: DataMember] TItem? Item
    ) : ICommand<Unit>
    where TItem : class, IHasId<string>
{
    public EditCommand(TItem value) : this(value.Id, value) { }
}

public interface IProductService: IComputeService
{
    [ComputeMethod]
    Task<Product?> Get(string id, CancellationToken cancellationToken = default);

    [CommandHandler]
    Task Edit(EditCommand<Product> command, CancellationToken cancellationToken = default);
}

public interface ICartService: IComputeService
{
    [ComputeMethod]
    Task<Cart?> Get(string id, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<decimal> GetTotal(string id, CancellationToken cancellationToken = default);

    [CommandHandler]
    Task Edit(EditCommand<Cart> command, CancellationToken cancellationToken = default);
}
