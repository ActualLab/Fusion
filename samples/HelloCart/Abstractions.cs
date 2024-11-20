using System.Runtime.Serialization;
using System.Transactions;
using ActualLab.CommandR.Operations;
using MemoryPack;
using MessagePack;
using Newtonsoft.Json;
using Pastel;

namespace Samples.HelloCart;

[DataContract, MemoryPackable, MessagePackObject]
[method: JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public partial record Product(
    [property: DataMember, Key(0)] string Id,
    [property: DataMember, Key(1)] decimal Price
) : IHasId<string>;

[DataContract, MemoryPackable, MessagePackObject]
[method: JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public partial record Cart(
    [property: DataMember, Key(0)] string Id
) : IHasId<string>
{
    [DataMember] public ImmutableDictionary<string, decimal> Items { get; init; } = ImmutableDictionary<string, decimal>.Empty;
}

[DataContract, MemoryPackable, MessagePackObject]
[method: JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public partial record EditCommand<TItem>(
    [property: DataMember, Key(0)] string Id,
    [property: DataMember, Key(1)] TItem? Item
) : ICommand<Unit>
    where TItem : class, IHasId<string>
{
    public EditCommand(TItem value) : this(value.Id, value) { }
}

[DataContract, MemoryPackable, MessagePackObject]
[method: JsonConstructor, MemoryPackConstructor, SerializationConstructor]
public partial record LogMessageCommand(
    [property: DataMember, Key(0)] Symbol Uuid,
    [property: DataMember, Key(1)] string Message,
    [property: DataMember, Key(2)] Moment DelayUntil = default
) : ILocalCommand<Unit>, IHasUuid, IHasDelayUntil
{
    private static long _nextIndex;

    public static LogMessageCommand New()
    {
        var now = Moment.Now;
        var delay = TimeSpan.FromSeconds((10*Random.Shared.NextDouble()) - 5).Positive();
        var index = Interlocked.Increment(ref _nextIndex);
        var message = delay > TimeSpan.FromMilliseconds(1)
            ? $"Message #{index}, triggered with {delay.ToShortString()} delay"
            : $"Message #{index}";
        return new(Ulid.NewUlid().ToString(), message, now + delay);
    }

    // This is ILocalCommand, so Run is its own handler
    public async Task Run(CommandContext context, CancellationToken cancellationToken)
    {
        var hasDelayUntil = DelayUntil != default;
        var color = hasDelayUntil ? ConsoleColor.Green : ConsoleColor.Blue;
        Console.WriteLine($"[{Uuid}] {Message}".Pastel(color));
        if (AppSettings.EnableRandomLogMessageCommandFailures && char.IsDigit(Uuid.Value[^1])) {
            await Task.Delay(300, CancellationToken.None).ConfigureAwait(false);
            throw new TransactionException("Can't run this command!");
        }
    }
}

public interface IProductService: IComputeService
{
    [ComputeMethod]
    public Task<Product?> Get(string id, CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task Edit(EditCommand<Product> command, CancellationToken cancellationToken = default);
}

public interface ICartService: IComputeService
{
    [ComputeMethod]
    public Task<Cart?> Get(string id, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<decimal> GetTotal(string id, CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task Edit(EditCommand<Cart> command, CancellationToken cancellationToken = default);
}
