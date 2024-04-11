using System.Runtime.Serialization;
using ActualLab.CommandR.Operations;
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

[DataContract, MemoryPackable]
[method: JsonConstructor, MemoryPackConstructor]
public partial record LogMessageCommand(
    [property: DataMember] Symbol Uuid,
    [property: DataMember] string Message,
    [property: DataMember] Moment FiresAt = default
) : ILocalCommand<Unit>, IHasUuid, IHasFiresAt
{
    private static long _nextIndex;

    public static LogMessageCommand NewDelayed()
    {
        var index = Interlocked.Increment(ref _nextIndex);
        var firesAt = SystemClock.Now + TimeSpan.FromSeconds((30*Random.Shared.NextDouble()) - 5);
        return new(Ulid.NewUlid().ToString(), $"#{index}: should fire at {firesAt.ToDateTime():T}", firesAt);
    }

    public Task Run(CommandContext context, CancellationToken cancellationToken)
    {
        var hasFiresAt = FiresAt != default;
        var delay = hasFiresAt ? $" (delay: {(SystemClock.Now - FiresAt).ToShortString()})" : "";
        var color = hasFiresAt ? ConsoleColor.DarkRed : ConsoleColor.DarkBlue;
        Console.BackgroundColor = color;
        Console.WriteLine($"[{Uuid}] {Message}{delay}");
        Console.ResetColor();
        if (false && char.IsDigit(Uuid.Value[^1]))
            throw new InvalidOperationException("Can't run this command!");
        return Task.CompletedTask;
    }
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
