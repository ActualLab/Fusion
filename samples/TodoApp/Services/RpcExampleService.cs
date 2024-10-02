using System.Runtime.CompilerServices;
using ActualLab.Rpc;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

public class RpcExampleService : IRpcExampleService
{
    private const double RowDelayProbability = 0.2;
    private const double ItemDelayProbability = 0.2;
    private static readonly RandomTimeSpan DelayDuration = TimeSpan.FromMilliseconds(300).ToRandom(0.5);
    private static readonly RandomTimeSpan SumDelayDuration = TimeSpan.FromMilliseconds(100).ToRandom(0.5);

    public Task<string> Greet(string name, CancellationToken cancellationToken = default)
        => Task.FromResult($"Hello, {name}!");

    public Task<Table<int>> GetTable(string title, CancellationToken cancellationToken = default)
    {
        var table = new Table<int>(title, RpcStream.New(GetRows(CancellationToken.None)));
        return Task.FromResult(table);
    }

    public async Task<int> Sum(RpcStream<int> stream, CancellationToken cancellationToken = default)
    {
        await Task.Delay(SumDelayDuration.Next(), cancellationToken);
        return await stream.SumAsync(cancellationToken).ConfigureAwait(false);
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
