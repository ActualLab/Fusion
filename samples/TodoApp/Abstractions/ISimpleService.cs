using System.Runtime.Serialization;
using ActualLab.Rpc;
using MessagePack;

namespace Samples.TodoApp.Abstractions;

public interface ISimpleService : IRpcService
{
    public Task<string> Greet(string name, CancellationToken cancellationToken = default);
    public Task<Table<int>> GetTable(string title, CancellationToken cancellationToken = default);
    public Task<int> Sum(RpcStream<int> stream, CancellationToken cancellationToken = default);
    public Task<RpcNoWait> Ping(string message);
}

[DataContract, MessagePackObject(true)]
public sealed partial record Table<T>(
    [property: DataMember(Order = 0)] string Title,
    [property: DataMember(Order = 1)] RpcStream<Row<T>> Rows);

[DataContract, MessagePackObject(true)]
public sealed partial record Row<T>(
    [property: DataMember(Order = 0)] int Index,
    [property: DataMember(Order = 1)] RpcStream<T> Items);
