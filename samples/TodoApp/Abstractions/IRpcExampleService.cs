using System.Runtime.Serialization;
using ActualLab.Rpc;
using MemoryPack;
using MessagePack;

namespace Samples.TodoApp.Abstractions;

public interface IRpcExampleService : IRpcService
{
    Task<string> Greet(string name, CancellationToken cancellationToken = default);
    Task<Table<int>> GetTable(string title, CancellationToken cancellationToken = default);
    Task<int> Sum(RpcStream<int> stream, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public sealed partial record Table<T>(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] string Title,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] RpcStream<Row<T>> Rows);

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
public sealed partial record Row<T>(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] int Index,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] RpcStream<T> Items);
