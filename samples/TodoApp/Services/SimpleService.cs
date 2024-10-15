using System.Runtime.CompilerServices;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

public class SimpleService(ISimpleClientSideService clientSideService) : ISimpleService
{
    private const double RowDelayProbability = 0.2;
    private const double ItemDelayProbability = 0.2;
    private static readonly RandomTimeSpan DelayDuration = TimeSpan.FromMilliseconds(300).ToRandom(0.5);

    public Task<string> Greet(string name, CancellationToken cancellationToken = default)
        => Task.FromResult($"Hello, {name}!");

    public Task<Table<int>> GetTable(string title, CancellationToken cancellationToken = default)
    {
        var table = new Table<int>(title, RpcStream.New(GetRows(CancellationToken.None)));
        return Task.FromResult(table);
    }

    public Task<int> Sum(RpcStream<int> stream, CancellationToken cancellationToken = default)
        => stream.SumAsync(cancellationToken).AsTask();

    public async Task<RpcNoWait> Ping(string message)
    {
        var peer = RpcInboundContext.GetCurrent().Peer; // Get the peer for the current call
        using var _ = new RpcOutboundContext(peer).Activate(); // Pre-routes the upcoming call to that peer
        await clientSideService.Pong($"Pong to '{message}'").ConfigureAwait(false);
        return default;
    }

    // Private methods

    private async IAsyncEnumerable<Row<int>> GetRows([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rnd = new Random();
        for (var i = 0;; i++) {
            if (rnd.NextDouble() <= RowDelayProbability)
                await Task.Delay(DelayDuration.Next(), cancellationToken).ConfigureAwait(false);
            var items = GetItems(i, CancellationToken.None);
            yield return new Row<int>(i, RpcStream.New(items));
        }
        // ReSharper disable once IteratorNeverReturns
    }

    private async IAsyncEnumerable<int> GetItems(int index, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rnd = new Random();
        for (var i = 0;; i++) {
            if (rnd.NextDouble() <= ItemDelayProbability)
                await Task.Delay(DelayDuration.Next(), cancellationToken).ConfigureAwait(false);
            yield return index * i;
        }
        // ReSharper disable once IteratorNeverReturns
    }
}
