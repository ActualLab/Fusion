using Microsoft.Extensions.Hosting;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services;

#pragma warning disable 1998

public class InMemoryStockApi : IStockApi, IHostedService, IDisposable
{
    private readonly ConcurrentDictionary<string, StockPrice> _prices = new(StringComparer.Ordinal);
    private readonly Timer _timer;
    private string[] _symbols = [];
    private bool _isDisposed;

    public InMemoryStockApi()
    {
        InitializeStocks();
        _timer = new Timer(UpdateRandomTicker, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _timer.Dispose();
    }

    // IHostedService

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer.Change(TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(0.25));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    // IStockApi - Queries

    public virtual Task<StockPrice?> Get(string symbol, CancellationToken cancellationToken = default)
        => Task.FromResult(_prices.GetValueOrDefault(symbol));

    public virtual async Task<string[]> ListSymbols(CancellationToken cancellationToken = default)
    {
        await PseudoListSymbols().ConfigureAwait(false);
        return _prices.Keys.Order().ToArray();
    }

    // Pseudo queries

    [ComputeMethod]
    protected virtual Task<Unit> PseudoListSymbols()
        => TaskExt.UnitTask;

    // Private methods

    private void InitializeStocks()
    {
        var stocks = new[] {
            ("ACME", "Acme Corporation", 150.00m),
            ("FUSE", "Fusion Technologies", 85.00m),
            ("BLZR", "Blazor Inc", 220.00m),
            ("RCTV", "Reactive Systems", 45.00m),
            ("COMP", "Computed Holdings", 180.00m),
            ("DATA", "DataSync Corp", 95.00m),
            ("SYNC", "SyncState Ltd", 130.00m),
            ("LIVE", "LiveUpdate Co", 75.00m),
        };

        var now = DateTime.UtcNow;
        foreach (var (symbol, name, price) in stocks) {
            var stock = new StockPrice(symbol, name, price, price, 0m, 0m, now);
            _prices[symbol] = stock;
        }
        _symbols = _prices.Keys.ToArray();
    }

    private void UpdateRandomTicker(object? state)
    {
        if (_isDisposed)
            return;

        // 50% chance to update a random ticker on each tick
        if (Random.Shared.NextDouble() > 0.5)
            return;

        var symbol = _symbols[Random.Shared.Next(_symbols.Length)];
        if (!_prices.TryGetValue(symbol, out var old))
            return;

        var now = DateTime.UtcNow;
        var changePercent = ((decimal)Random.Shared.NextDouble() - 0.5m) * 0.02m; // ±1% max change
        var newPrice = Math.Round(old.Price * (1m + changePercent), 2);
        var change = Math.Round(newPrice - old.OpenPrice, 2);
        var changePct = Math.Round((newPrice - old.OpenPrice) / old.OpenPrice * 100m, 2);

        var newStock = old with {
            Price = newPrice,
            Change = change,
            ChangePercent = changePct,
            UpdatedAt = now,
        };
        _prices[symbol] = newStock;

        using var invalidating = Invalidation.Begin();
        _ = Get(symbol, default);
    }
}
