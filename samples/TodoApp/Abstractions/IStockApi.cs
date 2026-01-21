using System.Runtime.Serialization;
using MemoryPack;
using MessagePack;

namespace Samples.TodoApp.Abstractions;

public interface IStockApi : IComputeService
{
    [ComputeMethod]
    public Task<string[]> ListSymbols(CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<StockPrice?> Get(string symbol, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable, MessagePackObject(true)]
public sealed partial record StockPrice(
    [property: DataMember] string Symbol,
    [property: DataMember] string Name,
    [property: DataMember] decimal Price,
    [property: DataMember] decimal OpenPrice,
    [property: DataMember] decimal Change,
    [property: DataMember] decimal ChangePercent,
    [property: DataMember] DateTime UpdatedAt
);
